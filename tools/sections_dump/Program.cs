// Usage: dotnet run --project tools/sections_dump -- <name.tpl> [<name.tpl_data>] > sections.tsv
// Columns: pool, part, texture, variant, indexCount, uniqueVerts, visible, hideReason, node, boundsMin, boundsMax.
// Pool "L0" is mesh.Parts (largest vertex pool); "LODn" are mesh.Levels. The exported LOD0 is the visible set.
// Feed the TSV to tools/rdc/map_sections_to_draws.py to match sections to capture draws by index count.
using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

var tpl = args[0];
var data = args.Length > 1 ? args[1] : Path.ChangeExtension(tpl, ".tpl_data");
var mesh = TemplateMeshReader.LoadRaw(File.ReadAllBytes(tpl), File.Exists(data) ? File.ReadAllBytes(data) : null, Path.GetFileNameWithoutExtension(tpl));

void Dump(string pool, List<Vector3> pos, List<int> idx, List<MeshPart> parts)
{
  foreach (var p in parts)
  {
    var verts = new HashSet<int>();
    var min = new Vector3(float.MaxValue);
    var max = new Vector3(float.MinValue);
    for (var i = p.IndexStart; i < p.IndexStart + p.IndexCount; i++)
    {
      verts.Add(idx[i]);
      min = Vector3.Min(min, pos[idx[i]]);
      max = Vector3.Max(max, pos[idx[i]]);
    }
    Console.WriteLine(string.Join('\t', pool, p.Name, p.Texture, p.Variant, p.IndexCount, verts.Count, p.Visible, p.HideReason,
      p.Node, $"{min.X:F2},{min.Y:F2},{min.Z:F2}", $"{max.X:F2},{max.Y:F2},{max.Z:F2}"));
  }
}

Dump("L0", mesh.Positions, mesh.Indices, mesh.Parts);
foreach (var level in mesh.Levels)
  Dump(level.Name, level.Positions, level.Indices, level.Parts);
Console.Error.WriteLine(mesh.Message);
