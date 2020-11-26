using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DBAnonymizer
{
    public class AnonymizerService
    {
        private readonly MessageService _messageService;
        private readonly IHttpClientFactory _clientFactory;
        private readonly Random _random;
        private string _randomUserUrl = "https://randomuser.me/api/";
        private readonly string _primaryKeyCommand = @"SELECT 
                                                   column_name as PRIMARYKEYCOLUMN
                                                 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS TC 
                                                 INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KU
                                                     ON TC.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                                                     AND TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME 
                                                     AND KU.table_name='{0}'

                                                 ORDER BY 
                                                      KU.TABLE_NAME
                                                     ,KU.ORDINAL_POSITION
                                                 ; ";
        public AnonymizerService(MessageService messageService, IHttpClientFactory clientFactory)
        {
            _messageService = messageService;
            _clientFactory = clientFactory;
            _random = new Random();
        }

        public async Task<IEnumerable<string>> ConnectToDatabase(string connectionString)
        {
            DataTable table = null!;
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    _messageService.SendMessage("Invalid connection string");
                    return new List<string>();
                }
                _messageService.SendMessage("Connecting...");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    _messageService.SendMessage($"Connected to {connection.Database}");
                    table = await connection.GetSchemaAsync("Tables", restrictionValues: new string[] { null!, null!, null!, "BASE TABLE" }).ConfigureAwait(false);
                }
                var result = new List<string>();
                foreach (DataRow row in table.Rows)
                {
                    result.Add($"{row["TABLE_SCHEMA"]?.ToString() ?? ""}.{row["TABLE_NAME"]?.ToString() ?? ""}");
                }
                return result;
            }
            catch (SqlException e)
            {
                _messageService.SendMessage(e.ToString());
            }
            return new List<string>();
        }

        public async Task ExecuteReplace(List<ReplaceObject> replaceObjects, string connectionString)
        {
            var maxRowCount = 0;
            if (replaceObjects == null)
            {
                _messageService.SendMessage("Nothing to replace!");
                return;
            }
            foreach (var replacer in replaceObjects)
            {
                using (var connection = new SqlConnection(connectionString))
                using (var countCommand = new SqlCommand($"SELECT COUNT(*) FROM {replacer.TableNameToSql()}", connection))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    var rows = (int)(await countCommand.ExecuteScalarAsync().ConfigureAwait(false) ?? 0);
                    if (maxRowCount < rows)
                    {
                        maxRowCount = rows;
                    }
                }
            }
            var names = new List<Person>();
            if (maxRowCount > 5000)
            {
                while (names.Count < maxRowCount)
                {
                    names.AddRange(await GetNames(5000).ConfigureAwait(false));
                }
            }
            else
            {
                names = await GetNames(maxRowCount).ConfigureAwait(false);
            }
            var commands = new List<string>();
            foreach (var replacer in replaceObjects)
            {
                var pkColumn = await GetPrimaryKeyColumn(replacer.TableName, connectionString).ConfigureAwait(false);
                var pkColumnType = await GetColumnProperties(pkColumn, replacer.TableName, connectionString).ConfigureAwait(false);
                int counter = 0;
                _messageService.SendMessage($"Starting replacements for {replacer.ColumnName}");
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand($"SELECT {replacer.ColumnName}, {pkColumn} FROM {replacer.TableNameToSql()} order by {pkColumn}", connection))
                using (var countCommand = new SqlCommand($"SELECT COUNT(*) FROM {replacer.TableNameToSql()}", connection))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                    if (!names.Any())
                    {
                        _messageService.SendMessage("COULD NOT CONTACT API, PLEASE TRY AGAIN!");
                        return;
                    }
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var value = "";
                            if (!await reader.IsDBNullAsync(reader.GetOrdinal(replacer.ColumnName)).ConfigureAwait(false))
                            {
                                value = reader.GetString(0).Replace("'", "''");
                            }
                            dynamic primaryKey; // can be either a string or an int
                            try
                            {
                                if (pkColumnType.Contains("char"))
                                {
                                    primaryKey = "'" + reader.GetString(pkColumn) + "'";
                                }
                                else
                                {
                                    primaryKey = reader.GetInt32(reader.GetOrdinal(pkColumn));
                                }
                            }
                            catch (InvalidCastException e)
                            {
                                _messageService.SendMessage(e.Message);
                                break;
                            }
                            catch (IndexOutOfRangeException e)
                            {
                                _messageService.SendMessage(e.Message);
                                break;
                            }
                            switch (replacer.ReplaceType)
                            {
                                case "Email":
                                    var mailAddress = $"{names[counter].name.first}.{names[counter].name.last}@nomail.com".Replace("'", "''");
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{mailAddress}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "First name":
                                    var firstName = $"{names[counter].name.first}".Replace("'", "''");
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{firstName}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "Last name":
                                    var lastName = $"{names[counter].name.last}".Replace("'", "''");
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{lastName}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "First and last name":
                                    var firstlast = $"{names[counter].name.first} {names[counter].name.last}".Replace("'", "''");
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{firstlast}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "Username":
                                    var username = names[counter].login.username.Replace("'", "''");
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{username}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "Phonenumber":
                                    var phone = names[counter].phone;
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{phone}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                case "Personal number (yyyymmdd-xxxx)":
                                    var pNumber = $"{names[counter].dob.date.ToString("yyyyMMdd")}-{_random.Next(1, 9)}{_random.Next(1, 9)}{_random.Next(1, 9)}{_random.Next(1, 9)}";
                                    commands.Add($"UPDATE {replacer.TableNameToSql()} SET {replacer.ColumnName} = '{pNumber}' WHERE {pkColumn} = {primaryKey}");
                                    break;
                                default:
                                    break;
                            }
                            counter++;
                            if (counter >= names.Count)
                            {
                                names.AddRange(await GetNames(200).ConfigureAwait(false));
                            }

                        }
                    }
                    await reader.CloseAsync().ConfigureAwait(false);
                }
            }
            using (var connection = new SqlConnection(connectionString))
            using (var commandConnection = new SqlConnection(connectionString))
            using (var replaceCommand = new SqlCommand())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                replaceCommand.Connection = connection;
                foreach (var command in commands)
                {
                    replaceCommand.CommandText = command;
                    await replaceCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            _messageService.SendMessage("Finshed database replacements.");
        }

        public async Task<IEnumerable<string>> GetColumns(string selectedTable, string connectionString)
        {
            _messageService.SendMessage("Connecting...");
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    var columnRestrictions = new string[4];
                    columnRestrictions[2] = selectedTable.Split(".").Last();
                    _messageService.SendMessage($"Fetching columns for table {selectedTable}");
                    var columns = await connection.GetSchemaAsync("Columns", columnRestrictions).ConfigureAwait(false);
                    var result = new List<string>();
                    foreach (DataRow row in columns.Rows)
                    {
                        result.Add(row["COLUMN_NAME"]?.ToString() ?? "");
                    }
                    return result;
                }
            }
            catch (Exception e)
            {
                _messageService.SendMessage(e.ToString());
            }
            return new List<string>();
        }

        public async Task<string> GetColumnProperties(string selectedColumn, string selectedTable, string connectionString)
        {
            var columnRestrictions = new string[4];
            columnRestrictions[2] = selectedTable.Split(".").Last();

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand($"SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{columnRestrictions[2]}' AND COLUMN_NAME = '{selectedColumn}'"))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                command.Connection = connection;
                var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                if (reader.HasRows)
                {
                    await reader.ReadAsync().ConfigureAwait(false);
                    return reader.GetString(0);
                }
                return "";
            }
        }

        public async Task<(string before, string after)> ShowExample(string selectedReplaceType, string selectedColumn, string selectedTable, string connectionString)
        {
            var names = await GetNames(1).ConfigureAwait(false);
            var before = "";
            var after = "";
            if (!names.Any())
            {
                _messageService.SendMessage("COULD NOT CONTACT API, PLEASE TRY AGAIN!");
                return ("", "");
            }
            var counter = 0;
            var example = "";
            switch (selectedReplaceType)
            {
                case "Email":
                    example = $"{names[counter].name.first}.{names[counter].name.last}@nomail.com";
                    break;
                case "First name":
                    example = $"{names[counter].name.first}";
                    break;
                case "Last name":
                    example = $"{names[counter].name.last}";
                    break;
                case "First and last name":
                    example = $"{names[counter].name.first} {names[counter].name.last}";
                    break;
                case "Username":
                    example = names[counter].login.username;
                    break;
                case "Phonenumber":
                    example = names[counter].phone;
                    break;
                case "Personal number (yyyymmdd-xxxx)":
                    example = $"{names[counter].dob.date.ToString("yyyyMMdd")}-{_random.Next(1, 9)}{_random.Next(1, 9)}{_random.Next(1, 9)}{_random.Next(1, 9)}";
                    break;
                default:
                    break;
            }

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand($"SELECT {selectedColumn} FROM {selectedTable}", connection))
            using (var replaceCommand = new SqlCommand())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                if (reader.HasRows)
                {
                    await reader.ReadAsync().ConfigureAwait(false);
                    var value = await reader.IsDBNullAsync(0).ConfigureAwait(false) ? "" : reader.GetString(0);
                    before = value;
                }
            }
            after = example;
            return (before, after);
        }

        private async Task<string> GetPrimaryKeyColumn(string tableName, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var primaryKeyCommand = new SqlCommand(string.Format(_primaryKeyCommand, tableName.Split(".").Last())))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                primaryKeyCommand.Connection = connection;
                var reader = await primaryKeyCommand.ExecuteReaderAsync().ConfigureAwait(false);
                if (reader.HasRows)
                {
                    await reader.ReadAsync().ConfigureAwait(false);
                    var value = await reader.IsDBNullAsync(0) ? "" : reader.GetString(0);
                    return value;
                }

                return "";
            }
        }

        private async Task<List<Person>> GetNames(int rows)
        {
            var counter = 0;
            while (counter < 50)
            {
                try
                {
                    var client = _clientFactory.CreateClient();
                    var result = await client.GetAsync($"{_randomUserUrl}?results={rows}").ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                        counter = 16;
                        var options = new JsonSerializerOptions
                        {
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        };
                        return JsonSerializer.Deserialize<RandomUserGeneratorResult>(content, options)?.results!;
                    }
                    else
                    {
                        counter++;
                        await Task.Delay(_random.Next(1000, 2000) * _random.Next(1, 3)).ConfigureAwait(false);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    counter++;
                }
            }
            return new List<Person>();
        }
    }
}