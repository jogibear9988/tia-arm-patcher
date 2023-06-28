﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine("TIA Portal ARM Patcher");
Console.WriteLine();

Console.WriteLine("Choose the version to patch:");
Console.WriteLine("1. V17");
Console.WriteLine("2. V18");

int choice;
string input = Console.ReadLine();
bool isValid = int.TryParse(input, out choice) && (choice == 1 || choice == 2);

while (!isValid)
{
    Console.WriteLine("Invalid choice. Please enter 1 or 2.");
    input = Console.ReadLine();
    isValid = int.TryParse(input, out choice) && (choice == 1 || choice == 2);
}

string tiaPath;

if (choice == 1)
{
    tiaPath = "C:\\Program Files\\Siemens\\Automation\\Portal V17\\Bin";
}
else // choice == 2
{
    tiaPath = "C:\\Program Files\\Siemens\\Automation\\Portal V18\\Bin";
}

Console.WriteLine("TIA Dir: " + tiaPath);
Console.WriteLine();

var patchedFiles = new List<string>();

using (var resolver = new DefaultAssemblyResolver())
{
    resolver.AddSearchDirectory(tiaPath);
    foreach (var file in Directory.GetFiles(tiaPath, "*.dll"))
    {
        try
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(file, new ReaderParameters { AssemblyResolver = resolver }))
            {
                var module = assembly.MainModule;
                
                if (module.GetTypes().Any(x => x.Name == "AssemblyIterator" || x.Name == "InternalAssemblyIterator"))
                {
                    var assemblyIteratorType = module.GetTypes().Where(x => x.Name == "AssemblyIterator").FirstOrDefault();
                    if (assemblyIteratorType != null)
                    {
                        var method = assemblyIteratorType.GetMethods().FirstOrDefault(x => x.Name == "CheckIfSignatureOfSingleAssemblyIsValid");

                        method.Body.InitLocals = true;
                        method.Body.Variables.Clear();
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Byte.MakeArrayType()));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Boolean));

                        var ilProcessor = method.Body.GetILProcessor();
                        var i = Instruction.Create(OpCodes.Ldloc_3);
                        ilProcessor.Clear();
                        ilProcessor.Append(Instruction.Create(OpCodes.Nop));
                        ilProcessor.Append(Instruction.Create(OpCodes.Ldc_I4_1));
                        ilProcessor.Append(Instruction.Create(OpCodes.Stloc_3));
                        ilProcessor.Append(Instruction.Create(OpCodes.Br_S, i));
                        ilProcessor.Append(i);
                        ilProcessor.Append(Instruction.Create(OpCodes.Ret));
                    }

                    var internalAssemblyIteratorType = module.GetTypes().Where(x => x.Name == "InternalAssemblyIterator").FirstOrDefault();
                    if (internalAssemblyIteratorType != null)
                    {
                        var method = internalAssemblyIteratorType.GetMethods().FirstOrDefault(x => x.Name == "CheckIfSignatureOfSingleAssemblyIsValid");

                        method.Body.InitLocals = true;
                        method.Body.Variables.Clear();
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Byte.MakeArrayType()));
                        method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Boolean));

                        var ilProcessor = method.Body.GetILProcessor();
                        var i = Instruction.Create(OpCodes.Ldloc_3);
                        ilProcessor.Clear();
                        ilProcessor.Append(Instruction.Create(OpCodes.Nop));
                        ilProcessor.Append(Instruction.Create(OpCodes.Ldc_I4_1));
                        ilProcessor.Append(Instruction.Create(OpCodes.Stloc_3));
                        ilProcessor.Append(Instruction.Create(OpCodes.Br_S, i));
                        ilProcessor.Append(i);
                        ilProcessor.Append(Instruction.Create(OpCodes.Ret));
                    }
                    var newFile = file.Substring(0, file.Length - 3) + ".patched";
                    if (File.Exists(newFile))
                        File.Delete(newFile);
                    assembly.Write(newFile);

                    patchedFiles.Add(file);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Process File: " + file);
                    Console.WriteLine("--> file was patched...");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                
                if (module.GetTypes().Any(x => x.Name == "OpenSslWrapperBio" && file.Contains("Open"))) 
                {

                    var assemblyIteratorType = module.GetTypes().Where(x => x.Name == "OpenSslWrapperBio").FirstOrDefault();
                    var assemblyIteratorTypeCallMethod = module.GetTypes().Where(x => x.Name == "OpenSslWrapperCertBase").LastOrDefault();
                    if (assemblyIteratorType != null)
                    {
                        var method = assemblyIteratorType.GetMethods().FirstOrDefault(x => x.Name == "Dispose");
                        var methodCallMethod = assemblyIteratorTypeCallMethod.GetMethods().Last(x => x.Name.Contains("Dispose"));


                        method.Body.ExceptionHandlers.Clear();
                        var ilProcessor = method.Body.GetILProcessor();
                        ilProcessor.Clear();
                        ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                        ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_1));
                        ilProcessor.Append(Instruction.Create(OpCodes.Call,methodCallMethod));
                        ilProcessor.Append(Instruction.Create(OpCodes.Ret));


                    }

                    

                    var newFile = file.Substring(0, file.Length - 3) + ".patched";
                    if (File.Exists(newFile))
                        File.Delete(newFile);
                    assembly.Write(newFile);

                    patchedFiles.Add(file);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Process File: " + file);
                    Console.WriteLine("--> file was patched...");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
        }
        catch (BadImageFormatException)
        {
            //Console.WriteLine("--> File is not an Assembly");
        }
        catch (IOException)
        {
            Console.WriteLine("Process File: " + file);
            Console.WriteLine("--> File is not accessible, this may be a problem if file is needed to be patched");
        }
    }


}

Console.WriteLine();
Console.WriteLine();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Patched " + patchedFiles.Count + " files");
foreach (var file in patchedFiles)
{
    Console.WriteLine("Patched: " + file);
    var backupFile =  file.Substring(0, file.Length - 3) + ".backup";
    var patchedFile = file.Substring(0, file.Length - 3) + ".patched";
    if (File.Exists(backupFile))
    {
        File.Delete(file);
    }
    else
    {
        File.Move(file, backupFile);
    }
    File.Move(patchedFile, file);
}
Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine();
Console.WriteLine("Finished patching, press Key to exit");

Console.ReadLine();