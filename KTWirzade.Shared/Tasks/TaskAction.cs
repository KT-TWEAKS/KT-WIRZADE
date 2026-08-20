using System.Globalization;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace KTWirzade.Shared.Tasks
{    
    public enum ErrorAction
    {
        Ignore,
        Log,
        Notify,
        Halt,
    }

    /// <summary>
    /// <b>True</b>: Run during ISO mastering, as well as during normal installation, but not during OOBE unless OOBE is set to True<br/>
    /// <b>Only</b>: Only run during ISO mastering<br/>
    /// <b>False</b> (default): Never run during ISO mastering
    /// </summary>
    public enum ISOSetting
    {
        True,
        Only,
        False,
    }

    /// <summary>
    /// <b>True</b>: Always run during OOBE, as well as during normal installation<br/>
    /// <b>Only</b>: Only run during OOBE<br/>
    /// <b>False</b>: Never run during OOBE<br/>
    /// <b>Null</b> (default): Run during normal installation, as well as OOBE unless ISO is set to True
    /// </summary>
    public enum OOBESetting
    {
        True,
        Only,
        False, 
    }

    public abstract class TaskAction
    {
        public enum ExitCodeAction
        {
            Log,
            Retry,
            Error,
            RetryError,
            Halt,
        }
        
        [YamlMember(typeof(ISOSetting), Alias = "iso")]
        public virtual ISOSetting ISO { get; set; } = ISOSetting.False;
        
        [YamlMember(typeof(OOBESetting?), Alias = "oobe")]
        public virtual OOBESetting? OOBE { get; set; } = null;
        
        [YamlMember(typeof(bool), Alias = "ignoreErrors")]
        public bool IgnoreErrors { get; set; } = false;
        
        [YamlMember(typeof(string), Alias = "option")]
        public string Option { get; set; } = null;
        
        [YamlMember(typeof(string), Alias = "status")]
        public string Status { get; set; } = null;
        
        [YamlMember(typeof(string[]), Alias = "options")]
        public string[] Options { get; set; } = null;
        
        [YamlMember(typeof(string[]), Alias = "builds")]
        public string[] Builds { get; set; } = null;
        
        [YamlMember(typeof(string), Alias = "cpuArch")]
        public string Arch { get; set; } = null;
        
        [YamlMember(typeof(bool?), Alias = "onUpgrade")]
        public bool? OnUpgrade { get; set; } = null;
                
        [YamlMember(typeof(string[]), Alias = "onUpgradeVersions")]
        public string[] OnUpgradeVersions { get; set; } = null;
        
        [YamlMember(typeof(string), Alias = "previousOption")]
        [CanBeNull] public string PreviousOption { get; set; } = null;
        
        [YamlMember(typeof(ErrorAction), Alias = "errorAction")]
        public ErrorAction? ErrorAction { get; set; } = null;
        [YamlMember(typeof(bool), Alias = "allowRetries")]
        public bool? AllowRetries { get; set; } = null;

        /// <returns>Null if compatible, otherwise a string representing the error</returns>
        public virtual string? IsISOCompatible() => null;
        protected bool IsApplicableNumber(string number, int value)
        {
            bool negative = false;
            number = number.Trim();
            if (number.StartsWith("!"))
            {
                number = number.TrimStart('!');
                negative = true;
            }
            bool result = false;

            if (number.StartsWith(">="))
            {
                var parsed = int.Parse(number.Substring(2), CultureInfo.InvariantCulture);
                if (value >= parsed)
                    result = true;
            }
            else if (number.StartsWith("<="))
            {
                var parsed = int.Parse(number.Substring(2), CultureInfo.InvariantCulture);
                if (value <= parsed)
                    result = true;
            }
            else if (number.StartsWith(">"))
            {
                var parsed = int.Parse(number.Substring(1), CultureInfo.InvariantCulture);
                if (value > parsed)
                    result = true;
            }
            else if (number.StartsWith("<"))
            {
                var parsed = int.Parse(number.Substring(1), CultureInfo.InvariantCulture);
                if (value < parsed)
                    result = true;
            }
            else
            {
                var parsed = int.Parse(number, CultureInfo.InvariantCulture);
                if (value == parsed)
                    result = true;
            }

            return negative ? !result : result;
        }
    }
}
