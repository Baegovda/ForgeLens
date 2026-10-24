using System.Globalization;
using System.Numerics;
using System.Text;

namespace LibSaber.SM2.Assets.Meshes;

public static class FbxWriter
{

  public static void Write(string path, ExportedMesh mesh, ExportSpace space)
  {
    var culture = CultureInfo.InvariantCulture;
    var builder = new StringBuilder();
    var next = 1000L;
    long Id() => next++;

    var geometry = mesh.HasTriangles ? Id() : 0;
    var model = Id();
    var boneIds = mesh.Bones.Select(_ => Id()).ToArray();
    var skin = mesh.HasSkin ? Id() : 0;
    var clusters = mesh.HasSkin ? mesh.Bones.Select(_ => Id()).ToArray() : Array.Empty<long>();

    builder.AppendLine("; FBX 7.4.0 project file");
    builder.AppendLine("; Created by LibSaber.SM2Browser");
    builder.AppendLine("FBXHeaderExtension:  {");
    builder.AppendLine("\tFBXHeaderVersion: 1003");
    builder.AppendLine("\tFBXVersion: 7400");
    builder.AppendLine("\tCreator: \"LibSaber.SM2Browser\"");
    builder.AppendLine("}");
    builder.AppendLine("GlobalSettings:  {");
    builder.AppendLine("\tVersion: 1000");
    builder.AppendLine("\tProperties70:  {");
    builder.AppendLine("\t\tP: \"UpAxis\", \"int\", \"Integer\", \"\",2");
    builder.AppendLine("\t\tP: \"UpAxisSign\", \"int\", \"Integer\", \"\",1");
    builder.AppendLine("\t\tP: \"FrontAxis\", \"int\", \"Integer\", \"\",1");
    builder.AppendLine("\t\tP: \"FrontAxisSign\", \"int\", \"Integer\", \"\",1");
    builder.AppendLine("\t\tP: \"CoordAxis\", \"int\", \"Integer\", \"\",0");
    builder.AppendLine("\t\tP: \"CoordAxisSign\", \"int\", \"Integer\", \"\",1");
    builder.AppendLine("\t\tP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1");
    builder.AppendLine("\t}");
    builder.AppendLine("}");
    builder.AppendLine("Objects:  {");

    if (geometry != 0)
    {
      builder.Append("\tGeometry: ").Append(geometry).Append(", \"Geometry::").Append(Safe(mesh.Name)).AppendLine("\", \"Mesh\" {");
      builder.Append("\t\tVertices: *").Append(mesh.Positions.Count * 3).Append(" {\n\t\t\ta: ");
      for (var i = 0; i < mesh.Positions.Count; i++)
      {
        var point = AxisConvert.Point(mesh.Positions[i], space, gltf: false);
        if (i > 0)
          builder.Append(',');
        builder.Append(point.X.ToString("R", culture)).Append(',').Append(point.Y.ToString("R", culture)).Append(',').Append(point.Z.ToString("R", culture));
      }

      builder.AppendLine("\n\t\t}");
      builder.Append("\t\tPolygonVertexIndex: *").Append(mesh.Indices.Count).Append(" {\n\t\t\ta: ");
      for (var i = 0; i < mesh.Indices.Count; i++)
      {
        var index = mesh.Indices[i];
        if ((i + 1) % 3 == 0)
          index = -index - 1;
        if (i > 0)
          builder.Append(',');
        builder.Append(index);
      }

      builder.AppendLine("\n\t\t}");
      builder.AppendLine("\t\tGeometryVersion: 124");
      if (mesh.Normals.Count == mesh.Positions.Count)
      {
        builder.AppendLine("\t\tLayerElementNormal: 0 {");
        builder.AppendLine("\t\t\tVersion: 101");
        builder.AppendLine("\t\t\tName: \"\"");
        builder.AppendLine("\t\t\tMappingInformationType: \"ByPolygonVertex\"");
        builder.AppendLine("\t\t\tReferenceInformationType: \"Direct\"");
        builder.Append("\t\t\tNormals: *").Append(mesh.Indices.Count * 3).Append(" {\n\t\t\t\ta: ");
        for (var i = 0; i < mesh.Indices.Count; i++)
        {
          var normal = Vector3.Normalize(AxisConvert.Direction(mesh.Normals[mesh.Indices[i]], gltf: false));
          if (float.IsNaN(normal.X))
            normal = Vector3.UnitZ;
          if (i > 0)
            builder.Append(',');
          builder.Append(normal.X.ToString("R", culture)).Append(',').Append(normal.Y.ToString("R", culture)).Append(',').Append(normal.Z.ToString("R", culture));
        }

        builder.AppendLine("\n\t\t\t}");
        builder.AppendLine("\t\t}");
      }

      builder.AppendLine("\t\tLayer: 0 {");
      builder.AppendLine("\t\t\tVersion: 100");
      if (mesh.Normals.Count == mesh.Positions.Count)
        builder.AppendLine("\t\t\tLayerElement:  { Type: \"LayerElementNormal\", TypedIndex: 0 }");
      builder.AppendLine("\t\t}");
      builder.AppendLine("\t}");
    }

    builder.Append("\tModel: ").Append(model).Append(", \"Model::").Append(Safe(mesh.Name)).AppendLine("\", \"Mesh\" {");
    builder.AppendLine("\t\tVersion: 232");
    builder.AppendLine("\t\tProperties70:  {");
    builder.AppendLine("\t\t\tP: \"RotationActive\", \"bool\", \"\", \"\",1");
    builder.AppendLine("\t\t\tP: \"InheritType\", \"enum\", \"\", \"\",1");
    builder.AppendLine("\t\t\tP: \"ScalingMax\", \"Vector3D\", \"Vector\", \"\",0,0,0");
    builder.AppendLine("\t\t\tP: \"DefaultAttributeIndex\", \"int\", \"Integer\", \"\",0");
    builder.AppendLine("\t\t}");
    builder.AppendLine("\t\tShading: T");
    builder.AppendLine("\t\tCulling: \"CullingOff\"");
    builder.AppendLine("\t}");

    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var bone = mesh.Bones[i];
      var translation = AxisConvert.Point(new Vector3(bone.Local.M41, bone.Local.M42, bone.Local.M43), space, gltf: false);
      builder.Append("\tModel: ").Append(boneIds[i]).Append(", \"Model::").Append(Safe(bone.Name)).AppendLine("\", \"LimbNode\" {");
      builder.AppendLine("\t\tVersion: 232");
      builder.AppendLine("\t\tProperties70:  {");
      builder.Append("\t\t\tP: \"Lcl Translation\", \"Lcl Translation\", \"\", \"A\",")
        .Append(translation.X.ToString("R", culture)).Append(',')
        .Append(translation.Y.ToString("R", culture)).Append(',')
        .Append(translation.Z.ToString("R", culture)).AppendLine();
      builder.AppendLine("\t\t\tP: \"RotationActive\", \"bool\", \"\", \"\",1");
      builder.AppendLine("\t\t\tP: \"InheritType\", \"enum\", \"\", \"\",1");
      builder.AppendLine("\t\t}");
      builder.AppendLine("\t}");
    }

    if (skin != 0)
    {
      builder.Append("\tDeformer: ").Append(skin).AppendLine(", \"Deformer::Skin\", \"Skin\" {");
      builder.AppendLine("\t\tVersion: 101");
      builder.AppendLine("\t\tLink_DeformAcuracy: 50");
      builder.AppendLine("\t}");
      for (var bone = 0; bone < mesh.Bones.Count; bone++)
      {
        var indexes = new List<int>();
        var weights = new List<float>();
        for (var vertex = 0; vertex < mesh.Skin.Count; vertex++)
        {
          var skinVertex = mesh.Skin[vertex];
          AddWeight(indexes, weights, vertex, skinVertex.J0, skinVertex.W0, bone);
          AddWeight(indexes, weights, vertex, skinVertex.J1, skinVertex.W1, bone);
          AddWeight(indexes, weights, vertex, skinVertex.J2, skinVertex.W2, bone);
          AddWeight(indexes, weights, vertex, skinVertex.J3, skinVertex.W3, bone);
        }

        if (indexes.Count == 0)
          continue;

        var inverse = AxisConvert.Matrix(mesh.Bones[bone].InverseBind, space, gltf: false);
        builder.Append("\tDeformer: ").Append(clusters[bone]).Append(", \"Deformer::").Append(Safe(mesh.Bones[bone].Name)).AppendLine("\", \"Cluster\" {");
        builder.AppendLine("\t\tVersion: 100");
        builder.Append("\t\tIndexes: *").Append(indexes.Count).Append(" {\n\t\t\ta: ").Append(string.Join(",", indexes)).AppendLine("\n\t\t}");
        builder.Append("\t\tWeights: *").Append(weights.Count).Append(" {\n\t\t\ta: ")
          .Append(string.Join(",", weights.Select(weight => weight.ToString("R", culture)))).AppendLine("\n\t\t}");
        builder.Append("\t\tTransform: *16 {\n\t\t\ta: ").Append(RowMajor(inverse, culture)).AppendLine("\n\t\t}");
        builder.AppendLine("\t}");
      }
    }

    builder.AppendLine("}");
    builder.AppendLine("Connections:  {");
    builder.Append("\tC: \"OO\",").Append(model).AppendLine(",0");
    if (geometry != 0)
      builder.Append("\tC: \"OO\",").Append(geometry).Append(',').Append(model).AppendLine();
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var parent = mesh.Bones[i].Parent;
      var parentId = parent >= 0 && parent < boneIds.Length ? boneIds[parent] : 0;
      builder.Append("\tC: \"OO\",").Append(boneIds[i]).Append(',').Append(parentId).AppendLine();
    }

    if (skin != 0)
    {
      builder.Append("\tC: \"OO\",").Append(skin).Append(',').Append(geometry).AppendLine();
      for (var i = 0; i < clusters.Length; i++)
      {
        var used = false;
        for (var vertex = 0; vertex < mesh.Skin.Count; vertex++)
        {
          var skinVertex = mesh.Skin[vertex];
          if ((skinVertex.J0 == i && skinVertex.W0 > 0) || (skinVertex.J1 == i && skinVertex.W1 > 0)
            || (skinVertex.J2 == i && skinVertex.W2 > 0) || (skinVertex.J3 == i && skinVertex.W3 > 0))
          {
            used = true;
            break;
          }
        }

        if (!used)
          continue;
        builder.Append("\tC: \"OO\",").Append(clusters[i]).Append(',').Append(skin).AppendLine();
        builder.Append("\tC: \"OO\",").Append(boneIds[i]).Append(',').Append(clusters[i]).AppendLine();
      }
    }

    builder.AppendLine("}");
    File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
  }

  private static void AddWeight(List<int> indexes, List<float> weights, int vertex, int joint, float weight, int bone)
  {
    if (joint == bone && weight > 0)
    {
      indexes.Add(vertex);
      weights.Add(weight);
    }
  }

  private static string RowMajor(Matrix4x4 matrix, CultureInfo culture)
  {
    float[] values =
    {
      matrix.M11, matrix.M12, matrix.M13, matrix.M14,
      matrix.M21, matrix.M22, matrix.M23, matrix.M24,
      matrix.M31, matrix.M32, matrix.M33, matrix.M34,
      matrix.M41, matrix.M42, matrix.M43, matrix.M44,
    };
    return string.Join(",", values.Select(value => value.ToString("R", culture)));
  }

  private static string Safe(string name)
  {
    var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray();
    var text = new string(chars);
    return text.Length == 0 ? "object" : text;
  }

}
