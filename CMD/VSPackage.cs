using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace VS_Local_Packages_Cleaner.CatalogJson
{
    public class VSPackage
    {
        public string id { get; set; }
        public string version { get; set; }
        public string type { get; set; }
        public Dependencies dependencies { get; set; }
        public string language { get; set; }
        public List<LocalizedResource> localizedResources { get; set; }
        public string chip { get; set; }
        public string productArch { get; set; }
        public List<Payload> payloads { get; set; }
        public MsiProperties msiProperties { get; set; }
        public string productCode { get; set; }
        public string upgradeCode { get; set; }
        public string productVersion { get; set; }
        public long? productLanguage { get; set; }
        public string providerKey { get; set; }
        public InstallSizes installSizes { get; set; }
        public List<LogFile> logFiles { get; set; }
        public InstallParams installParams { get; set; }
        public RepairParams repairParams { get; set; }
        public UninstallParams uninstallParams { get; set; }
        public string relativePath { get; set; }
        public DetectConditions detectConditions { get; set; }
        public string vsixId { get; set; }
        public string extensionDir { get; set; }
        public Requirements requirements { get; set; }
        public string license { get; set; }
        public bool? isUiGroup { get; set; }
        public string machineArch { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(id);
            if (!string.IsNullOrWhiteSpace(version))
                sb.Append(",version=" + version);
            if (!string.IsNullOrWhiteSpace(chip))
                sb.Append(",chip=" + chip);
            if (!string.IsNullOrWhiteSpace(language))
                sb.Append(",language=" + language);
            if (!string.IsNullOrWhiteSpace(productArch))
                sb.Append(",productarch=" + productArch);
            if (!string.IsNullOrWhiteSpace(machineArch))
                sb.Append(",machinearch=" + machineArch);
            return sb.ToString();
        }
    }

    public class Dependencies
    {

    }

    public class LocalizedResource
    {
        public string language { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string category { get; set; }
        public List<string> keywords { get; set; }
    }

    public class Payload
    {
        public string fileName { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
        public string url { get; set; }
        public Signer signer { get; set; }
        public bool? isDynamicEndpoint { get; set; }
    }

    public class Signer
    {
        [JsonProperty("$id")]
        public string id { get; set; }
        public string subjectName { get; set; }
    }

    public class MsiProperties
    {
        public string MSIFASTINSTALL { get; set; }
        public string VSEXTUI { get; set; }
    }

    public class InstallSizes
    {
        public long systemDrive { get; set; }
        public long? sharedDrive { get; set; }
        public long? targetDrive { get; set; }
    }

    public class LogFile
    {
        public string pattern { get; set; }
    }

    public class InstallParams
    {
        public string fileName { get; set; }
        public string parameters { get; set; }
    }
    public class UninstallParams
    {
        public string fileName { get; set; }
        public string parameters { get; set; }
    }
    public class RepairParams
    {
        public string fileName { get; set; }
        public string parameters { get; set; }
    }

    public class DetectConditions
    {
        public List<Condition> conditions { get; set; }
        public string expression { get; set; }
    }
    public class Condition
    {
        public string filePath { get; set; }
        public string registryKey { get; set; }
        public string id { get; set; }
        public string registryValue { get; set; }
        public string join { get; set; }
        public string registryData { get; set; }
    }

    public class Requirements
    {
        public Functors functors { get; set; }
        public string supportedOS { get; set; }
    }

    public class Functors
    {
        public string architecture { get; set; }
    }
}
