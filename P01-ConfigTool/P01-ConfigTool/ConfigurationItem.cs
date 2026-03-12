namespace P01_ConfigTool
{
    public class ConfigurationItem
    {
        private string _configName = string.Empty;
        private string _configValue = string.Empty;
        private string _dataType = "string";
        private string _description = string.Empty;
        private bool _isLoaded = false; // track loading state

        public int ConfigID { get; set; }

        public string ConfigName
        {
            get => _configName;
            set
            {
                _configName = value ?? string.Empty; // set default if null
                if (_isLoaded)
                {
                    NeedsSaving = true; // update needs saving flag if edited
                }
            }
        }

        public string ConfigValue
        {
            get => _configValue;
            set
            {
                _configValue = value ?? string.Empty; // set default if null
                if (_isLoaded)
                {
                    NeedsSaving = true; // update needs saving flag if edited
                }
            }
        }

        public string DataType
        {
            get => _dataType;
            set
            {
                _dataType = value ?? "string"; // set default if null
                if (_isLoaded)
                {
                    NeedsSaving = true; // update needs saving flag if edited
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value ?? string.Empty; // set default if null
                if (_isLoaded)
                {
                    NeedsSaving = true; // update needs saving flag if edited
                }
            }
        }

        public bool NeedsSaving { get; set; } = false; // set false to start

        // call after populating data grid so that it knows not to set needs saving flags for loads
        public void MarkLoaded()
        {
            _isLoaded = true;
        }
    }
}
