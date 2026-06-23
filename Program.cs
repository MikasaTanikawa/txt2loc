// See https://aka.ms/new-console-template for more information
// If can't connect to nuget.org, read this: https://devblogs.microsoft.com/dotnet/deprecating-tls-1-0-and-1-1-on-nuget-org/
using System.Reflection;
using System.Text;
using System.Xml;
using static System.Console;
using static System.Reflection.Assembly;

var assemblyName = GetExecutingAssembly()?.GetName()?.Name ?? "Who am I?";
var version = GetExecutingAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
const string T2G_STARTLOC = "#";
const string T2G_ENDLOC = "--";
const string T2G_ENDLOCEND = " ---------------------------------";
const string T2G_NEWLINE = "\r\n";
const string LOCS_FILE = "locations.xml";
const string LOC_EXT = ".txt";

try
{
    if (args.Length > 0)
    {
        bool decompose = true;
        string? input = null;
        string? output = null;
        string? project = null;
        string? startLoc = T2G_STARTLOC;
        string? endLoc = T2G_ENDLOC;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("-"))
            {
                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                        WriteHelp();
                        break;
                    case "-d":
                        break;
                    case "-c":
                        decompose = false;
                        break;
                    case "-p":
                        if (++i < args.Length)
                        {
                            project = TrimQuotations(args[i]);

                            if (!string.IsNullOrWhiteSpace(project))
                                break;
                        }
                        WriteErrorAndHelp("Expected path to .qproj file.");
                        return 1;
                    case "-s":
                        if (++i < args.Length)
                        {
                            startLoc = TrimQuotations(args[i]);

                            if (!string.IsNullOrWhiteSpace(startLoc))
                                break;
                        }
                        WriteErrorAndHelp("Expected 'Start of loc' prefix.");
                        return 1;
                    case "-e":
                        if (++i < args.Length)
                        {
                            endLoc = TrimQuotations(args[i]);

                            if (!string.IsNullOrWhiteSpace(endLoc))
                                break;
                        }
                        WriteErrorAndHelp("Expected 'End of loc' prefix.");
                        return 1;
                }
            }
            else
            {
                if (input == null)
                    input = TrimQuotations(arg);
                else
                    output ??= TrimQuotations(arg);
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            WriteErrorAndHelp("Expected <input> argument.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(output))
        {
            WriteErrorAndHelp("Expected <output> argument.");
            return 1;
        }

        if (decompose)
            return Decompose(input, output, project, startLoc, endLoc);
        else
            return Compose(input, output, project, startLoc, endLoc);
    }
    else
    {
        WriteHelp();
        return 1;
    }
}
catch (Exception e)
{
    WriteError(e.ToString());
    return -1;
}

void WriteHelp()
{
    var assembly = assemblyName.ToLowerInvariant();

    // Try to be this wide:
    //        $"-------------------------------------------------------------------------------\n"
    WriteLine();
    WriteLine($"{assemblyName.ToUpperInvariant()} v{version}");
    WriteLine($"Usage:");
    WriteLine($"  {assembly} [options] <input> <output>");
    WriteLine($"Options:");
    WriteLine($"  -h          Show this help");
    WriteLine($"  -d          Decompose single <input> text file to separate location files\n" +
              $"              and put them into <output> directory (default);");
    WriteLine($"              Also will create in <output> directory (or copy from .qproj file\n" +
              $"              if used '-p') '{LOCS_FILE}' file storing list of locations;");
    WriteLine($"              WARNING: <output> directory will be DELETED and recreated without\n" +
              $"              any promt for confirmation!");
    WriteLine($"  -c          Compose separate location files from <input> directory into\n" +
              $"              single <output> text file;");
    WriteLine($"              Also will update '{LOCS_FILE}' file in <input> directory\n" +
              $"              (and copy it into .qproj file if used '-p')");
    WriteLine($"  -p <value>  .qproj file");
    WriteLine($"  -s <value>  'Start of loc' prefix (default: '{T2G_STARTLOC}')");
    WriteLine($"  -e <value>  'End of loc' prefix (default: '{T2G_ENDLOC}')");
    WriteLine($"Examples:");
    WriteLine($"  {assembly} -d -p game.qproj game.txt locations");
    WriteLine($"  {assembly} -c -p \"my game.qproj\" locations \"my game.txt\"");
}

string? TrimQuotations(string? str)
{
    if (!string.IsNullOrEmpty(str) && str.StartsWith('"') && str.EndsWith('"'))
        return str[1..^1];
    else
        return str;
}

void WriteError(string error)
{
    WriteLine($"Error! {error}");
}

void WriteErrorAndHelp(string error)
{
    WriteError(error);
    WriteHelp();
}

int Decompose(string input, string output, string? project, string startLoc, string endLoc)
{
    if (!File.Exists(input))
    {
        WriteError($"Can't find or open '{input}' file for current directory '{Directory.GetCurrentDirectory()}'.");
        return 1;
    }

    if (Directory.Exists(output))
        Directory.Delete(output, true);
    var outputDir = Directory.CreateDirectory(output);

    var projectLocs = ReadLocationsFile(project);
    var locationsFile = Path.Combine(output, LOCS_FILE);

    if (projectLocs == null)
        return 1;

    if (!string.IsNullOrWhiteSpace(project) && File.Exists(project))
        File.Copy(project, locationsFile);

    using (var reader = new StreamReader(input, true))
    {
        reader.Peek();

        var encoding = reader.CurrentEncoding;
        string? str;
        int strNum = 0;
        bool firstLocLine = true;
        string? loc = null;
        int startLocLen = startLoc.Length;
        StreamWriter? writer = null;

        while ((str = reader.ReadLine()) != null)
        {
            strNum++;
            // This could fail miserably to find start of location with default T2G_STARTLOC ("#") value
            if (str.StartsWith(startLoc))
            {
                if (loc != null)
                {
                    WriteError($"At {strNum} line: found start of location before end of '{loc}' location!");
                    return 1;
                }

                loc = str.Remove(0, startLocLen).Trim();

                if (string.IsNullOrWhiteSpace(loc))
                {
                    loc = null;
                    WriteError($"At {strNum} line: found start of location, but can't read name of it!");
                    return 1;
                }

                if (projectLocs.TryGetValue(loc, out string? folder) && folder != null)
                    outputDir.CreateSubdirectory(folder);

                writer = new StreamWriter(GetLocationFilePath(output, folder, loc), false, encoding);
                firstLocLine = true;
            }
            // This could fail miserably to find end of location with default T2G_ENDLOC "--" value
            else if (str.StartsWith(endLoc) /*&& str == $"{endLoc} {loc}{T2G_ENDLOCEND}"*/)
            {
                loc = null;
                writer?.Close();
                writer?.Dispose();
            }
            else if (loc != null)
            {
                if (firstLocLine)
                {
                    firstLocLine = false;
                    writer?.Write(str);
                }
                else
                    writer?.Write($"{T2G_NEWLINE}{str}");
            }
        }
    }

    if (projectLocs.Count == 0)
        return CreateLocationsFile(locationsFile);
    else
        return UpdateLocationsFile(locationsFile);
}

int Compose(string input, string output, string? project, string startLoc, string endLoc)
{
    if (!Directory.Exists(input))
    {
        WriteError($"Can't find or open '{input}' directory for current directory '{Directory.GetCurrentDirectory()}'.");
        return 1;
    }

    var locationsFile = Path.Combine(input, LOCS_FILE);

    int updateResult = UpdateLocationsFile(locationsFile);
    if (updateResult != 0)
        return updateResult;

    if (!string.IsNullOrWhiteSpace(project))
        File.Copy(locationsFile, project, true);

    var locs = ReadLocationsFile(locationsFile);
    if (locs == null)
        return 1;

    Encoding encoding = new UTF8Encoding(true);

    // Detecting encoding in first file
    using (var reader = new StreamReader(GetLocationFilePath(input, locs.First().Value, locs.First().Key), true))
    {
        reader.Peek();
        encoding = reader.CurrentEncoding;
    }

    using var writer = new StreamWriter(output, false, encoding);
    foreach (var loc in locs)
    {
        using var reader = new StreamReader(GetLocationFilePath(input, loc.Value, loc.Key), true);

        writer.Write($"{startLoc} {loc.Key}{T2G_NEWLINE}");
        var str = reader.ReadToEnd();
        if (!string.IsNullOrEmpty(str))
            writer.Write($"{str}{T2G_NEWLINE}");
        writer.Write($"{endLoc} {loc.Key}{T2G_ENDLOCEND}{T2G_NEWLINE}{T2G_NEWLINE}");
    }

    return 0;
}

// Reads XML file and returns dictionary with location names in keys and folder names (or null if location isn't in folder) in values.
// Returns empty dictionary if project parameter is null.
// Returns null if can't find or read XML file or file doesn't have /QGen-project/Structure node.
Dictionary<string, string?>? ReadLocationsFile(string? locationsFile)
{
    var locs = new Dictionary<string, string?>();

    if (!string.IsNullOrWhiteSpace(locationsFile))
    {
        if (!File.Exists(locationsFile))
        {
            WriteError($"Can't find or open '{locationsFile}' file for current directory '{Directory.GetCurrentDirectory()}'.");
            return null;
        }
        else
        {
            var doc = new XmlDocument();

            doc.Load(locationsFile);

            var checkNodes = doc.SelectSingleNode("/QGen-project/Structure");

            if (checkNodes == null)
            {
                WriteError($"Can't find Structure node in '{locationsFile}' file.");
                return null;
            }

            var locNodes = doc.SelectNodes("//Location");

            if (locNodes == null)
                WriteError($"Can't find any Location nodes in '{locationsFile}' file."); // not fatal error
            else
            {
                foreach (XmlNode node in locNodes)
                {
                    string? name = node.Attributes?["name"]?.Value;

                    if (!string.IsNullOrEmpty(name))
                    {
                        string? folder = null;

                        if (node.ParentNode?.Name == "Folder")
                            folder = node.ParentNode?.Attributes?["name"]?.Value;

                        locs.Add(name, folder);
                    }
                }
            }
        }
    }

    return locs;
}

string GetLocationFilePath(string directory, string? subdirectory, string location)
{
    if (subdirectory == null)
        return Path.Combine(directory, location + LOC_EXT);
    else
        return Path.Combine(directory, subdirectory, location + LOC_EXT);
}

int CreateLocationsFile(string locationsFile)
{
    var dir = Path.GetDirectoryName(locationsFile);

    if (!Directory.Exists(dir))
    {
        WriteError($"Can't find or open '{dir}' directory for current directory '{Directory.GetCurrentDirectory()}'.");
        return 1;
    }

    using var ms = new MemoryStream();
    using var writer = XmlWriter.Create(ms, GetXmlWriterSettings());
    var doc = new XmlDocument();
    var files = Directory.GetFiles(dir, $"*{LOC_EXT}", SearchOption.AllDirectories);

    doc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<QGen-project version=\"4.0.0 beta 1\">" +
                "  <Structure/>" +
                "</QGen-project>");

    foreach (var file in files)
        XmlAddLocation(doc, dir, file);

    doc.Save(writer);
    WriteMemoryStreamToXml(locationsFile, ms, GetXmlWriterSettings());

    return 0;
}

int UpdateLocationsFile(string locationsFile)
{
    var projectLocs = ReadLocationsFile(locationsFile);

    if (projectLocs == null)
        return 1;

    var dir = Path.GetDirectoryName(locationsFile);

    if (!Directory.Exists(dir))
    {
        WriteError($"Can't find or open '{dir}' directory for current directory '{Directory.GetCurrentDirectory()}'.");
        return 1;
    }

    using var ms = new MemoryStream();
    using var writer = XmlWriter.Create(ms, GetXmlWriterSettings());
    var doc = new XmlDocument();
    doc.Load(locationsFile);

    // Remove deleted or moved locations from xml file
    foreach (var loc in projectLocs)
    {
        if (!File.Exists(GetLocationFilePath(dir, loc.Value, loc.Key)))
        {
            var node = doc.SelectSingleNode($"//Location[@name='{loc.Key}']");

            node?.ParentNode?.RemoveChild(node);
        }
    }

    // Remove deleted folders from xml file
    var folders = doc.SelectNodes("//Folder");
    if (folders != null)
    {
        foreach (XmlNode folder in folders)
        {
            string? folderName = folder.Attributes?["name"]?.Value;
            if (folderName == null || !folder.HasChildNodes && !Directory.Exists(Path.Combine(dir, folderName)))
                folder.ParentNode?.RemoveChild(folder);
        }
    }

    // Add new locations into xml file
    foreach (var file in Directory.GetFiles(dir, $"*{LOC_EXT}", SearchOption.AllDirectories))
    {
        string? loc = Path.GetFileNameWithoutExtension(file);
        var node = doc.SelectSingleNode($"//Location[@name='{loc}']");

        if (node == null)
            XmlAddLocation(doc, dir, file);
    }

    // Add new empty folders into xml file
    foreach (var folder in Directory.GetDirectories(dir))
    {
        if (!Directory.EnumerateFiles(folder).Any())
            XmlAddFolder(doc, doc.SelectSingleNode("//Structure"), new DirectoryInfo(folder).Name);
    }

    doc.Save(writer);
    WriteMemoryStreamToXml(locationsFile, ms, GetXmlWriterSettings());

    return 0;
}

static XmlWriterSettings GetXmlWriterSettings()
{
    return new XmlWriterSettings
    {
        Indent = true,
        NewLineChars = "\n",
        Encoding = new UTF8Encoding(false)
    };
}

void WriteMemoryStreamToXml(string xmlFile, MemoryStream ms, XmlWriterSettings settings)
{
    var str = settings.Encoding.GetString(ms.ToArray());

    // https://github.com/dotnet/runtime/issues/109629#issuecomment-2594962381
    // 'implement XmlWriter yourself', my ass!
    // should be setting for this crap
    str = str.Replace($" />{settings.NewLineChars}", $"/>{settings.NewLineChars}");

    if (str.EndsWith(" />"))
        str = str[0..^3] + $"/>{settings.NewLineChars}";
    else if (!str.EndsWith(settings.NewLineChars))
        str += settings.NewLineChars;

    File.WriteAllText(xmlFile, str);
}

static void XmlAddLocation(XmlDocument doc, string dir, string locationFile)
{
    var structureNode = doc.SelectSingleNode("//Structure");
    string? locDir = Path.GetDirectoryName(locationFile);
    XmlNode? folderNode = null;
    XmlElement? node = null;

    if (locDir != null && locDir != dir)
        folderNode = XmlAddFolder(doc, structureNode, new DirectoryInfo(locDir).Name);

    node = doc.CreateElement("Location");
    node.SetAttribute("name", Path.GetFileNameWithoutExtension(locationFile));
    if (folderNode == null)
        structureNode?.AppendChild(node);
    else
        folderNode?.AppendChild(node);
}

static XmlNode? XmlAddFolder(XmlDocument doc, XmlNode? structureNode, string folder)
{
    var folderNode = structureNode?.SelectSingleNode($"Folder[@name='{folder}']");

    if (folderNode == null)
    {
        var node = doc.CreateElement("Folder");
        node.SetAttribute("name", folder);
        folderNode = structureNode?.AppendChild(node);
    }

    return folderNode;
}