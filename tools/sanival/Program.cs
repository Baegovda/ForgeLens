// Bulk .sani validator: decodes every .sani under a folder (with its sibling .sani_data)
// through SaniReader.FromBytes and writes one TSV row per file.
// Usage: sanival <folder> <out.tsv>
// Columns: path, status, version, bones, frames, tracks, nan, maxStepDeg (abs-dot between
// consecutive rotation keys), scaleTracks, rootKeys, additive, notes.
using System.Numerics;
using LibSaber.SM2.Assets.Animation;

if (args.Length < 2)
{
  Console.Error.WriteLine("usage: sanival <folder> <out.tsv>");
  return 2;
}

if (args[0] == "--dump")
{
  // sanival --dump <file.sani> <bone> : print every key of one track.
  var b = File.ReadAllBytes(args[1]);
  var d = File.Exists(args[1] + "_data") ? File.ReadAllBytes(args[1] + "_data") : null;
  var c = SaniReader.FromBytes(b, d);
  foreach (var n in c.Notes) Console.WriteLine("# " + n);
  var t = c.Tracks.FirstOrDefault(x => x.BoneName == args[2]) ?? c.Tracks[int.Parse(args[2])];
  for (var i = 0; i < Math.Max(t.Rotation.Count, Math.Max(t.Translation.Count, t.Scale.Count)); i++)
    Console.WriteLine($"{i} T {(i < t.Translation.Count ? t.Translation[i].Value : default)} R {(i < t.Rotation.Count ? t.Rotation[i].Value : default)} S {(i < t.Scale.Count ? t.Scale[i].Value : default)}");
  return 0;
}

var root = Path.GetFullPath(args[0]);
using var output = new StreamWriter(args[1]);
output.WriteLine("path\tstatus\tversion\tbones\tframes\ttracks\tnan\tmaxStepDeg\tscaleTracks\trootKeys\tadditive\tnotes");
int ok = 0, fail = 0;
foreach (var file in Directory.EnumerateFiles(root, "*.sani", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
  var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
  var bytes = File.ReadAllBytes(file);
  var dataPath = file + "_data";
  var data = File.Exists(dataPath) ? File.ReadAllBytes(dataPath) : null;
  var version = bytes.Length >= 6 ? BitConverter.ToUInt16(bytes, 4) : 0;
  var frames = bytes.Length >= 24 ? BitConverter.ToInt32(bytes, 20) : 0;
  AnimationClip clip;
  try
  {
    clip = SaniReader.FromBytes(bytes, data, Path.GetFileNameWithoutExtension(file));
  }
  catch (Exception ex)
  {
    output.WriteLine($"{relative}\tFAIL\t{version}\t0\t{frames}\t0\t0\t0\t0\t0\t0\texception {ex.GetType().Name}: {ex.Message}");
    fail++;
    continue;
  }

  var nan = 0;
  var maxStep = 0.0;
  foreach (var track in clip.Tracks)
  {
    foreach (var key in track.Translation)
      if (!float.IsFinite(key.Value.X + key.Value.Y + key.Value.Z)) nan++;
    foreach (var key in track.Scale)
      if (!float.IsFinite(key.Value.X + key.Value.Y + key.Value.Z)) nan++;
    for (var i = 0; i < track.Rotation.Count; i++)
    {
      var q = track.Rotation[i].Value;
      if (!float.IsFinite(q.X + q.Y + q.Z + q.W)) { nan++; continue; }
      if (i == 0) continue;
      var dot = Math.Min(1.0, Math.Abs((double)Quaternion.Dot(track.Rotation[i - 1].Value, q)));
      maxStep = Math.Max(maxStep, 2 * Math.Acos(dot) * 180 / Math.PI);
    }
  }

  var status = clip.HasCurves && nan == 0 ? "OK" : "FAIL";
  if (status == "OK") ok++; else fail++;
  var scaleTracks = clip.Tracks.Count(t => t.Scale.Count > 0);
  var notes = string.Join(" | ", clip.Notes).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
  output.WriteLine($"{relative}\t{status}\t{version}\t{clip.Bones.Count}\t{frames}\t{clip.Tracks.Count}\t{nan}\t{maxStep:0.0}\t{scaleTracks}\t{clip.RootTranslation.Count}\t{(clip.HasAdditive ? 1 : 0)}\t{notes}");
}

Console.WriteLine($"OK {ok} FAIL {fail}");
return fail == 0 ? 0 : 1;
