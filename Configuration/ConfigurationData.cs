using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml.Linq;
using System.Reflection;
using System.IO;
using System.CodeDom;
using System.Runtime;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PiKvmLibrary.Configuration
{
    public class ConfigurationData<T> where T : class, new()
    {
        private T _ApplicationConfiguration;
        public T ApplicationConfiguration
        {
            get { return _ApplicationConfiguration; }
            set { _ApplicationConfiguration = value; }
        }

    
        private XmlSchemaSet _SchemaSet;
        public ConfigurationData()
        {
            string targetNamespace = @"PiKvmLibrary.local/PiKvmConnectionsConfigurationSchema.xsd";
            string embeddedResourceName = "PiKvmConnectionsConfigurationSchema.xsd";
            _SchemaSet = LoadSchemaSet(targetNamespace, null, embeddedResourceName, embeddedResourceName);
            ImportXml();                                                      
        }
        private bool ImportXml()
        {
            string xmlName = "PiKvmApiCommandsConfiguration.xml";
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    Schemas = _SchemaSet,
                    IgnoreWhitespace = true,
                    IgnoreComments = true,
                    CloseInput = true,
                    ConformanceLevel = ConformanceLevel.Auto
                };
                settings.ValidationEventHandler += ValidationCallback;

                Assembly assembly = Assembly.GetExecutingAssembly();
                DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(assembly.Location));
                if (!TryImportXmlFile(new FileInfo(Path.Combine(di.FullName, xmlName)), settings))
                    if (!TryImportXmlFromEmbeddedResource(xmlName, settings))
                        throw new Exception($"Could not find embedded XML resource: {xmlName}");
            }
            catch
            {
                throw new Exception("Could not load XML configuration document.");
            }
            if (_ApplicationConfiguration == null)
                throw new Exception("Failed to deserialize XML configuration document.");
            return true;
        }
        private bool TryImportXmlFromEmbeddedResource(string xmlName, XmlReaderSettings settings)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();

                string resourceFullName = resourceNames.FirstOrDefault(r => r.Contains(xmlName));
                if (string.IsNullOrEmpty(resourceFullName))
                    return false;

                using (var stream = assembly.GetManifestResourceStream(resourceFullName))
                {
                    if (stream == null) { }


                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    using (XmlReader reader = XmlReader.Create(stream, settings))
                    {
                        _ApplicationConfiguration = (T)serializer.Deserialize(reader);
                    }
                    //_ImportLogger?.Information("XML file imported successfully: {FileName}", xmlFileInfo.FullName);
                    return true;
                }
            }
            catch (Exception ex)
            {

            }
            return false;
        }
        private bool TryImportXmlFile(FileInfo xmlFileInfo, XmlReaderSettings settings)
        {
            if (xmlFileInfo == null || !xmlFileInfo.Exists)
            {
                //_ImportLogger?.Error("XML file does not exist: {FileName}", xmlFileInfo?.FullName);
                return false;
            }
            //_ImportLogger?.Information("Importing XML file: {FileName}", xmlFileInfo.FullName);
            if (xmlFileInfo.Extension.ToLower() != ".xml")
            {
                //_ImportLogger?.Error("Invalid XML file extension: {Extension}", xmlFileInfo.Extension);
                return false;
            }
            if (xmlFileInfo.Length == 0)
            {
                //_ImportLogger?.Error("XML file is empty: {FileName}", xmlFileInfo.FullName);
                return false;
            }
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (XmlReader reader = XmlReader.Create(xmlFileInfo.FullName, settings))
                {
                    _ApplicationConfiguration = (T)serializer.Deserialize(reader);
                }
                //_ImportLogger?.Information("XML file imported successfully: {FileName}", xmlFileInfo.FullName);
                return true;
            }
            catch (XmlException ex)
            {
                //_ImportLogger?.Error("XML import error: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                //_ImportLogger?.Error("Unexpected error during XML import: {Message}", ex.Message);
            }
            return false;
        }

        private XmlSchemaSet LoadSchemaSet(string targetNamespace, string schemaUri, string fallbackFileName, string embeddedResourceName)
        {
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.ValidationEventHandler += XsdValidationCallback;

            if (schemaUri != null && TryLoadSchemaFromUri(schemaSet, targetNamespace, schemaUri)) { }
            //_ImportLogger?.Information("Loaded schema from web.");
            else if (fallbackFileName != null && TryLoadFromLocalFile(schemaSet, targetNamespace, fallbackFileName)) { }
            //_ImportLogger?.Information("Loaded schema from local file.");
            else if (embeddedResourceName != null && TryLoadFromEmbeddedResource(schemaSet, targetNamespace, embeddedResourceName)) { }
            //_ImportLogger?.Information("Loaded schema from embedded resource.");
            else
                throw new Exception("Could not load schema from any source.");

            return schemaSet;
        }

        private bool TryLoadSchemaFromUri(XmlSchemaSet schemaSet, string targetNamespace, string schemaUrl)
        {
            try
            {
                XmlSchema loadedSchema = schemaSet.Add(targetNamespace, schemaUrl);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryLoadFromLocalFile(XmlSchemaSet schemaSet, string targetNamespace, string fileName)
        {
            try
            {
                string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string fullPath = Path.Combine(exePath, fileName);
                if (!File.Exists(fullPath))
                    return false;

                using (var reader = XmlReader.Create(fullPath))
                {
                    schemaSet.Add(targetNamespace, reader);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryLoadFromEmbeddedResource(XmlSchemaSet schemaSet, string targetNamespace, string resourceName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();

                string resourceFullName = resourceNames.FirstOrDefault(r => r.Contains(resourceName));
                if (string.IsNullOrEmpty(resourceFullName))
                    return false;
                using (var stream = assembly.GetManifestResourceStream(resourceFullName))
                {
                    if (stream == null)
                        return false;

                    using (var reader = XmlReader.Create(stream))
                    {
                        schemaSet.Add(targetNamespace, reader);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void XsdValidationCallback(object sender, ValidationEventArgs e)
        {
            if (e.Severity == XmlSeverityType.Warning) { }
            //_ImportLogger?.Warning("Import XSD validation warning: {Message}", e.Message);
            else if (e.Severity == XmlSeverityType.Error) { }
            //_ImportLogger?.Error("Import XSD validation error: {Message}", e.Message);
        }
        private void ValidationCallback(object sender, ValidationEventArgs e)
        {
            if (e.Severity == XmlSeverityType.Warning) { }
                //_ImportLogger?.Warning("Import XML Validation warning: {Message}", e.Message);
            else if (e.Severity == XmlSeverityType.Error) { }
                //_ImportLogger?.Error("Import XML Validation error: {Message}", e.Message);
        }
    }
}
