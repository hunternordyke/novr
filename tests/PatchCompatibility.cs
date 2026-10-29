using Mono.Cecil;

internal static class PatchCompatibility
{
    public static void Verify(string modPath, string gameManagedDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameManagedDirectory);
        resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(gameManagedDirectory, "..", "..", "BepInEx", "core")));
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath))!);
        using var mod = AssemblyDefinition.ReadAssembly(modPath, new ReaderParameters { AssemblyResolver = resolver });
        var failures = new List<string>();
        var verified = 0;
        foreach (var type in AllTypes(mod.MainModule.Types))
        foreach (var patch in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
        {
            var target = patch.ConstructorArguments.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault();
            var method = patch.ConstructorArguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
            if (target == null || method == null) continue;
            var definition = target.Resolve();
            var signature = patch.ConstructorArguments.FirstOrDefault(a => a.Type.FullName == "System.Type[]").Value as CustomAttributeArgument[];
            var candidates = definition.Methods.Where(m => m.Name == method);
            if (signature != null)
                candidates = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName)
                    .SequenceEqual(signature.Select(a => ((TypeReference)a.Value).FullName)));
            if (!candidates.Any())
            {
                failures.Add($"Missing method or overload {target.FullName}.{method}");
                continue;
            }
            foreach (var parameter in type.Methods.SelectMany(m => m.Parameters).Where(p => p.Name.StartsWith("___")))
            {
                var fieldName = parameter.Name[3..];
                var owner = definition;
                while (owner != null && !owner.Fields.Any(f => f.Name == fieldName)) owner = owner.BaseType?.Resolve();
                if (owner == null) failures.Add($"Missing injected field {target.FullName}.{fieldName}");
            }
            verified++;
        }
        // Native guidance is mirrored read-only. Verify the private fields used
        // by that bridge as well as the Harmony-injected fields above.
        using var game = AssemblyDefinition.ReadAssembly(Path.Combine(gameManagedDirectory, "Assembly-CSharp.dll"));
        foreach (var contract in new[] {
            (owner: "CombatHUD", field: "weaponState", type: "HUDWeaponState"),
            (owner: "CombatHUD", field: "markers", type: "System.Collections.Generic.List`1<HUDUnitMarker>"),
            (owner: "TacScreen", field: "cam", type: "UnityEngine.Camera"),
            (owner: "TacScreen", field: "canvas", type: "UnityEngine.Canvas"),
            (owner: "TacScreen", field: "screenMaterial", type: "UnityEngine.Material"),
            (owner: "TargetCam", field: "cam", type: "UnityEngine.Camera"),
            (owner: "TargetCam", field: "currentMode", type: "TargetCam/CamMode"),
            (owner: "TargetCam", field: "canvasObjectTarget", type: "UnityEngine.GameObject"),
            (owner: "TargetCam", field: "canvasObjectLanding", type: "UnityEngine.GameObject"),
            (owner: "HUDMissileState", field: "hint", type: "TMPro.TextMeshProUGUI"),
            (owner: "HUDLaserGuidedState", field: "hint", type: "TMPro.TextMeshProUGUI") })
        {
            var field = game.MainModule.Types.Single(t => t.FullName == contract.owner).Fields.SingleOrDefault(f => f.Name == contract.field);
            if (field?.FieldType.FullName != contract.type) failures.Add($"Native readout field mismatch {contract.owner}.{contract.field}");
        }
        if (verified < 20) throw new Exception($"Only {verified} patch targets were checked; wrong build?");
        var selector = game.MainModule.Types.Single(t => t.Name == "CombatHUD").Methods.Single(m => m.Name == "TargetSelect").Body.Instructions;
        var acquisitionSites = selector.Count(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_R4 && i.Operand is float f && f == 100 &&
            i.Next?.Operand is MethodReference call && call.DeclaringType.Name == "FastMath" && call.Name == "InRange");
        if (acquisitionSites != 1) failures.Add("Native optical selection acquisition-circle IL contract changed.");
        if (failures.Count != 0) throw new Exception(string.Join(Environment.NewLine, failures));
        Console.WriteLine($"Verified {verified} declared patch targets (metadata only; no game code executed).");
    }

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
        }
    }
}
