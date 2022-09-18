namespace DBAnonymizer;

/// <summary>
/// Classes for Json serialization of data from https://randomuser.me/
/// </summary>
public class RandomUserGeneratorResult
{
    public List<Person>? results { get; set; } = new List<Person>();
}

public class Person
{
    public string gender { get; set; } = "";
    public Name name { get; set; } = new Name();
    public Location location { get; set; } = new Location();
    public string email { get; set; } = "";
    public Login login { get; set; } = new Login();
    public Dob dob { get; set; } = new Dob();
    public Registered registered { get; set; } = new Registered();
    public string phone { get; set; } = "";
    public string cell { get; set; } = "";
    public Id id { get; set; } = new Id();
    public Picture picture { get; set; } = new Picture();
    public string nat { get; set; } = "";
}

public class Name
{
    public string title { get; set; } = "";
    public string first { get; set; } = "";
    public string last { get; set; } = "";
}

public class Location
{
    public Street street { get; set; } = new Street();
    public string city { get; set; } = "";
    public string state { get; set; } = "";

    [JsonConverter(typeof(AutoNumberToStringConverter))]
    public string postcode { get; set; } = "";

    public Coordinates coordinates { get; set; } = new Coordinates();
    public Timezone timezone { get; set; } = new Timezone();
}

public class Street
{
    public string name { get; set; } = "";
    public int? number { get; set; }
}

public class Coordinates
{
    public string latitude { get; set; } = "";
    public string longitude { get; set; } = "";
}

public class Timezone
{
    public string offset { get; set; } = "";
    public string description { get; set; } = "";
}

public class Login
{
    public string uuid { get; set; } = "";
    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public string salt { get; set; } = "";
    public string md5 { get; set; } = "";
    public string sha1 { get; set; } = "";
    public string sha256 { get; set; } = "";
}

public class Dob
{
    public DateTime date { get; set; }
    public int age { get; set; }
}

public class Registered
{
    public DateTime date { get; set; }
    public int age { get; set; }
}

public class Id
{
    public string name { get; set; } = "";
    public string value { get; set; } = "";
}

public class Picture
{
    public string large { get; set; } = "";
    public string medium { get; set; } = "";
    public string thumbnail { get; set; } = "";
}