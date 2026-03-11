namespace P01_ConfigTool
{
    public class ConfigurationItem
    {
        public int ConfigID { get; set; }
        public string ConfigName { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";
        public string? Description { get; set; }
    }
}
