using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine("TIA Portal ARM Patcher");
Console.WriteLine();

var tiaPath = "C:\\Program Files\\Siemens\\Automation\\Portal V18\\Bin";

Console.WriteLine("TIA Dir: " + tiaPath);
Console.WriteLine();

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(tiaPath);

foreach (var file in Directory.GetFiles(tiaPath, "*.dll"))
{
    try
    {
        Console.WriteLine("Process File: " + file);
        using (var assembly = AssemblyDefinition.ReadAssembly(file, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true }))
        {
            var module = assembly.MainModule;

            var assemblyIteratorType = module.GetTypes().Where(x => x.Name == "AssemblyIterator" || x.Name == "InternalAssemblyIterator").FirstOrDefault();
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
                assembly.Write();
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("--> file was patched...");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
            {
                Console.WriteLine("--> File has no Type named: AssemblyIterator");
            }
        }
    }
    catch (BadImageFormatException)
    {
        Console.WriteLine("--> File is not an Assembly");
    }
    catch (IOException)
    {
        Console.WriteLine("--> File is not accessible, maybe this is not a problem");
    }
}

Console.WriteLine();
Console.WriteLine("Finished patching, press Key to exit");

Console.ReadLine();