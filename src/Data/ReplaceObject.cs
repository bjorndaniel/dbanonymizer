namespace DBAnonymizer;

public class ReplaceObject
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string ReplaceType { get; set; } = "";
    public string TableNameToSql() => TableName.Split(".").Aggregate((a, b) => $"[{a}].[{b}]");
    public List<ItemToKeep> ValuesToKeep { get; set; } = new();
}

public class ItemToKeep
{
    public string Value { get; set; } = "";
}