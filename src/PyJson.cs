using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SLAL2OStim;

/// <summary>
/// Writes JSON exactly like Python's json.dumps(data, indent=4, ensure_ascii=False)
/// (so the C# app and the Python script produce identical scene files).
/// </summary>
public static class PyJson
{
    public const string NL = "\r\n";

    public static string Dump(JsonNode node)
    {
        var sb = new StringBuilder();
        Write(sb, node, 0);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, JsonNode node, int level)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                if (obj.Count == 0) { sb.Append("{}"); break; }
                sb.Append('{').Append(NL);
                int i = 0;
                foreach (var kv in obj)
                {
                    Indent(sb, level + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    Write(sb, kv.Value, level + 1);
                    if (++i < obj.Count) sb.Append(',');
                    sb.Append(NL);
                }
                Indent(sb, level);
                sb.Append('}');
                break;
            case JsonArray arr:
                if (arr.Count == 0) { sb.Append("[]"); break; }
                sb.Append('[').Append(NL);
                for (int j = 0; j < arr.Count; j++)
                {
                    Indent(sb, level + 1);
                    Write(sb, arr[j], level + 1);
                    if (j + 1 < arr.Count) sb.Append(',');
                    sb.Append(NL);
                }
                Indent(sb, level);
                sb.Append(']');
                break;
            default:
                switch (node.GetValueKind())
                {
                    case JsonValueKind.String: WriteString(sb, node.GetValue<string>()); break;
                    case JsonValueKind.True: sb.Append("true"); break;
                    case JsonValueKind.False: sb.Append("false"); break;
                    case JsonValueKind.Null: sb.Append("null"); break;
                    default: sb.Append(node.ToJsonString()); break;
                }
                break;
        }
    }

    static void Indent(StringBuilder sb, int level) => sb.Append(' ', level * 4);

    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
