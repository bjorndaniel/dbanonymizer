namespace DBAnonymizer;

public class DBAnonymizerConfigs
{
    public List<DBAnonymizerConfig> Configs { get; set; } = new List<DBAnonymizerConfig>();
}

public class DBAnonymizerConfig
{
    public string Name { get; set; } = "";
    public List<ReplaceObject> ReplaceObjects { get; set; } = new List<ReplaceObject>();
}