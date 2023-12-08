using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine("TIA Portal ARM Patcher");
Console.WriteLine();

const string TIARootPath = "C:\\Program Files\\Siemens\\Automation";
const string TIAx86RootPath = "C:\\Program Files (x86)\\Siemens\\Automation";
bool x86PathExists = false;
bool nonx86PathExists = false;

List<string> tiaVersions = new();
if (Directory.Exists(TIAx86RootPath))
{
    var range = Directory.GetDirectories(TIAx86RootPath).Where(x => x.Contains("Portal") && Directory.Exists(Path.Combine(x, "bin")));
    tiaVersions.AddRange(range);
    x86PathExists = range.Any();
}
if (Directory.Exists(TIARootPath))
{
    var range = Directory.EnumerateDirectories(TIARootPath).Where(x => x.Contains("Portal") && Directory.Exists(Path.Combine(x, "bin")));
    tiaVersions.AddRange(range);
    nonx86PathExists = range.Any();
}

if (tiaVersions.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("No TIA Portal installations found, exiting...");
    Console.ForegroundColor = ConsoleColor.Gray;
    return;
}

Console.WriteLine("Choose the version to patch:");
for(int i = 0; i < tiaVersions.Count; i++)
{
    Console.WriteLine($"  {i}.\t{tiaVersions[i]}");
}
Console.WriteLine();
if ( x86PathExists && nonx86PathExists )
{
    Console.WriteLine($"  x86\tPatch all x86 versions detected");
    Console.WriteLine($"  x64\tPatch all x64 versions detected");
}
Console.WriteLine($"  ALL\tPatch all versions detected");
Console.WriteLine($"  ?\tDisplay this list again");

int choice = -1;
bool patchAll = false;
bool patchx86 = false;
bool patchx64 = false;
bool isValid = false;

do
{
    var input = Console.ReadLine();
    patchAll = (input == "ALL");
    patchx86 = (patchAll || input == "x86");
    patchx64 = (patchAll || input == "x64");

    bool displayHelp = (input == "?");
    isValid = (int.TryParse(input, out choice) && (choice < tiaVersions.Count)) || patchAll || patchx86 || patchx64;
    if (displayHelp)
    {
        for (int i = 0; i < tiaVersions.Count; i++)
        {
            Console.WriteLine($"{i}\t{tiaVersions[i]}");
        }
        Console.WriteLine();
        if (x86PathExists && nonx86PathExists)
        {
            Console.WriteLine($"  x86\tPatch all x86 versions detected");
            Console.WriteLine($"  x64\tPatch all x64 versions detected");
        }
        Console.WriteLine($"ALL\tPatch all versions detected");
        Console.WriteLine($"?\tDisplay this list again");
    }
    else
    {
        if (!isValid)
            Console.WriteLine($"Invalid choice. Please enter a choice between 0 and {tiaVersions.Count - 1}, or ALL for all.");
    }
} while (!isValid);

List<string> patchPaths = new();
if (patchAll)
    patchPaths.AddRange(tiaVersions.Select(x => Path.Combine(x, "bin")));
else if (patchx86)
    patchPaths.AddRange(tiaVersions.Where(x => x.StartsWith(TIAx86RootPath)).Select(x => Path.Combine(x,"bin")));
else if (patchx64)
    patchPaths.AddRange(tiaVersions.Where(x => x.StartsWith(TIARootPath)).Select(x => Path.Combine(x, "bin")));
else
    patchPaths = new List<string>{ Path.Combine(tiaVersions[choice], "bin") };


var patchedFiles = new List<string>();
foreach (var tiaPath in patchPaths)
{
    Console.WriteLine("TIA Dir: " + tiaPath);

    using var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(tiaPath);
    foreach (var file in Directory.GetFiles(tiaPath, "*.dll"))
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(file, new ReaderParameters { AssemblyResolver = resolver });
            var module = assembly.MainModule;
            bool filePatched = false;

            foreach (var assemblyIteratorType in module.GetTypes().Where(x => x.Name == "AssemblyIterator" || x.Name == "InternalAssemblyIterator"))
            {
                filePatched = true;
                var method = assemblyIteratorType.GetMethods().First(x => x.Name == "CheckIfSignatureOfSingleAssemblyIsValid");

                method.Body.InitLocals = true;
                method.Body.Variables.Clear();
                method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Byte.MakeArrayType()));
                method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Boolean));

                var ilProcessor = method.Body.GetILProcessor();
                ilProcessor.Clear();
                ilProcessor.Append(Instruction.Create(OpCodes.Nop));
                ilProcessor.Append(Instruction.Create(OpCodes.Ldc_I4_1));
                ilProcessor.Append(Instruction.Create(OpCodes.Stloc_3));
                var i = Instruction.Create(OpCodes.Ldloc_3);
                ilProcessor.Append(Instruction.Create(OpCodes.Br_S, i));
                ilProcessor.Append(i);
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));
            }

            if (module.GetTypes().Any(x => x.Name == "OpenSslWrapperBio" && file.Contains("Open")))
            {
                filePatched = true;
                var assemblyIteratorType = module.GetTypes().Where(x => x.Name == "OpenSslWrapperBio").FirstOrDefault();
                var assemblyIteratorTypeCallMethod = module.GetTypes().Where(x => x.Name == "OpenSslWrapperCertBase").LastOrDefault();
                if (assemblyIteratorType != null)
                {
                    var method = assemblyIteratorType.GetMethods().First(x => x.Name == "Dispose");
                    var methodCallMethod = assemblyIteratorTypeCallMethod.GetMethods().Last(x => x.Name.Contains("Dispose"));

                    method.Body.ExceptionHandlers.Clear();
                    var ilProcessor = method.Body.GetILProcessor();
                    ilProcessor.Clear();
                    ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                    ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_1));
                    ilProcessor.Append(Instruction.Create(OpCodes.Call, methodCallMethod));
                    ilProcessor.Append(Instruction.Create(OpCodes.Ret));
                }
            }

            if (filePatched)
            {
                var newFile = Path.ChangeExtension(file, ".patched");
                if (File.Exists(newFile))
                    File.Delete(newFile);
                assembly.Write(newFile);

                patchedFiles.Add(file);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\tProcess File: " + file);
                Console.WriteLine("\t--> file was patched...");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
        catch (BadImageFormatException)
        {
            //Console.ForegroundColor = ConsoleColor.Red;
            //Console.WriteLine("Process File: " + file);
            //Console.WriteLine("--> File is not an Assembly");
            //Console.ForegroundColor = ConsoleColor.Gray;
        }
        catch (IOException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Process File: " + file);
            Console.WriteLine("--> File is not accessible, this may be a problem if file is needed to be patched");
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine("Patched " + patchedFiles.Count + " files");
Console.Write("Do you confirm you want to overwrite TIA files (Y/N): ");
if (Console.ReadKey().Key == ConsoleKey.Y)
{
    Console.WriteLine();
    foreach (var file in patchedFiles)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Patched: " + file);
        var backupFile = Path.ChangeExtension(file, ".backup");
        var patchedFile = Path.ChangeExtension(file, ".patched");
        int backupNum = 1;
        while (File.Exists(backupFile))
            backupFile = Path.ChangeExtension(file, $".backup{backupNum}");
        File.Move(file, backupFile);
        File.Move(patchedFile, file);
    }
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine();
    Console.WriteLine("Finished patching, press Key to exit");
}
else
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Patching aborted, press Key to exit");
    Console.ForegroundColor = ConsoleColor.Gray;
}
Console.ReadLine();

