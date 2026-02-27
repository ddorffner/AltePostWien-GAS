using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Main;

public static class ComfyPngUtils
{
    /// <summary>
    /// Core extractor: returns workflow/prompt JSON strings from a PNG/JPEG buffer.
    /// </summary>
    private static void ExtractComfyJsonStrings(
        byte[] image,
        out string workflowJson,
        out string promptJson,
        out string error)
    {
        workflowJson = string.Empty;
        promptJson = string.Empty;
        error = string.Empty;

        if (image is null || image.Length < 8)
        {
            error = "Invalid or empty image buffer.";
            return;
        }

        try
        {
            var texts = ExtractPngTextChunks(image);
            var chunkSummary = GetChunkTypeSummary(image);

            // 1) Direct keys first
            if (texts.TryGetValue("workflow", out var wj) && !string.IsNullOrWhiteSpace(wj))
                workflowJson = TryExtractLargestJsonString(wj) ?? wj;
            if (texts.TryGetValue("prompt", out var pj) && !string.IsNullOrWhiteSpace(pj))
                promptJson = TryExtractLargestJsonString(pj) ?? pj;

            // 2) Fallback: scan values for "workflow\0{...}" or "prompt\0{...}"
            if (string.IsNullOrWhiteSpace(workflowJson) || string.IsNullOrWhiteSpace(promptJson))
            {
                foreach (var kv in texts)
                {
                    var text = kv.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(workflowJson))
                    {
                        var w = TryExtractAfterMarker(text, "workflow\0");
                        if (!string.IsNullOrWhiteSpace(w)) workflowJson = w;
                    }
                    if (string.IsNullOrWhiteSpace(promptJson))
                    {
                        var p = TryExtractAfterMarker(text, "prompt\0");
                        if (!string.IsNullOrWhiteSpace(p)) promptJson = p;
                    }
                    if (!string.IsNullOrWhiteSpace(workflowJson) && !string.IsNullOrWhiteSpace(promptJson)) break;
                }
            }

            // 3) Last resort: scan any text payload for the largest JSON object
            //    and classify it as workflow or prompt by structure
            if (string.IsNullOrWhiteSpace(workflowJson) || string.IsNullOrWhiteSpace(promptJson))
            {
                foreach (var kv in texts)
                {
                    var text = kv.Value ?? string.Empty;
                    if (TryClassifyJson(text, out var json, out var isWorkflow))
                    {
                        if (isWorkflow)
                        {
                            if (string.IsNullOrWhiteSpace(workflowJson)) workflowJson = json;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(promptJson)) promptJson = json;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(workflowJson) && !string.IsNullOrWhiteSpace(promptJson)) break;
                }
            }

            if (string.IsNullOrWhiteSpace(workflowJson) && string.IsNullOrWhiteSpace(promptJson))
            {
                string genericDiag = string.Empty;
                if (chunkSummary == "<not a PNG>")
                {
                    // Try a generic scan for non-PNG formats (e.g., JPEG with comments/XMP)
                    TryGenericScanForComfyJson(image, out var gw, out var gp, out genericDiag);
                    if (!string.IsNullOrWhiteSpace(gw)) workflowJson = gw;
                    if (!string.IsNullOrWhiteSpace(gp)) promptJson = gp;
                }

                if (string.IsNullOrWhiteSpace(workflowJson) && string.IsNullOrWhiteSpace(promptJson))
                {
                    var keys = string.Join(", ", texts.Keys);
                    var previews = BuildTextDiagnostics(texts, 160);
                    error =
                        "No ComfyUI JSON found in PNG metadata (checked tEXt/iTXt/zTXt).\n" +
                        $"Chunk types: {chunkSummary}\n" +
                        $"Found text keys: [{keys}]\n" +
                        (string.IsNullOrEmpty(previews) ? string.Empty : $"Text previews:\n{previews}") +
                        (string.IsNullOrEmpty(genericDiag) ? string.Empty : $"Generic scan:\n{genericDiag}");
                }
            }
        }
        catch (Exception ex)
        {
            error = $"[PARSER CRASH]\n{ex}";
        }

        if (string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(workflowJson) && string.IsNullOrWhiteSpace(promptJson))
        {
            error = "[DIAGNOSTICS] Parser finished without finding JSON and without producing an error message. This may indicate an unusual file structure.";
        }
    }

    /// <summary>
    /// Single public entry: extracts and returns both JSON strings and parsed objects.
    /// - workflowObj/promptObj are parsed from workflowJson/promptJson (empty JObject if parse fails)
    /// - If both strings are empty, error contains diagnostics
    /// </summary>
    public static void ComfyPngParser(
        byte[] image,
        out JObject workflowObj,
        out JObject promptObj,
        out string workflowJson,
        out string promptJson,
        out string error)
    {
        workflowObj = new JObject();
        promptObj = new JObject();
        workflowJson = string.Empty;
        promptJson = string.Empty;

        if (image == null || image.Length == 0)
        {
            error = "[FATAL] Input image buffer is null or empty. The file might be unreadable, corrupted, or not read correctly before this node.";
            return;
        }

        error = String.Empty;
        try
        {
            ExtractComfyJsonStrings(image, out workflowJson, out promptJson, out error);
            workflowObj = TryParseObject(workflowJson);
            promptObj = TryParseObject(promptJson);
        }
        catch (Exception ex)
        {
            error = $"[ComfyPngParser CRASH]\n{ex}";
        }
    }

    // ---------- PNG text extraction ----------
    private static Dictionary<string, string> ExtractPngTextChunks(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // PNG signature
        if (data.Length < 8 ||
            data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
            data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
        {
            return result;
        }

        int offset = 8;
        while (offset + 8 <= data.Length)
        {
            uint len = ReadUInt32BigEndian(data, offset);
            offset += 4;

            if (offset + 4 > data.Length) break;
            string type = Encoding.ASCII.GetString(data, offset, 4);
            offset += 4;

            if (offset + len + 4 > data.Length) break; // data + CRC must fit

            if (type == "tEXt")
            {
                Extract_tEXt(result, data, offset, (int)len);
            }
            else if (type == "zTXt")
            {
                Extract_zTXt(result, data, offset, (int)len);
            }
            else if (type == "iTXt")
            {
                Extract_iTXt(result, data, offset, (int)len);
            }

            offset += (int)len; // skip data
            offset += 4;        // skip CRC

            if (type == "IEND") break;
        }

        return result;
    }

    private static uint ReadUInt32BigEndian(byte[] data, int index)
    {
        return (uint)((data[index] << 24) | (data[index + 1] << 16) | (data[index + 2] << 8) | data[index + 3]);
    }

    private static void Extract_tEXt(Dictionary<string, string> dict, byte[] data, int offset, int length)
    {
        int end = offset + length;
        int sep = IndexOfByte(data, offset, length, 0x00);
        if (sep < 0) return;
        var keyword = Encoding.Latin1.GetString(data, offset, sep - offset);
        var value = Encoding.Latin1.GetString(data, sep + 1, end - (sep + 1));
        AddIfNotEmpty(dict, keyword, value);
    }

    private static void Extract_zTXt(Dictionary<string, string> dict, byte[] data, int offset, int length)
    {
        int end = offset + length;
        int sep = IndexOfByte(data, offset, length, 0x00);
        if (sep < 0 || sep + 1 >= end) return;
        var keyword = Encoding.Latin1.GetString(data, offset, sep - offset);
        byte compressionMethod = data[sep + 1];
        int compStart = sep + 2;
        if (compStart >= end) return;
        var compressed = new byte[end - compStart];
        Buffer.BlockCopy(data, compStart, compressed, 0, compressed.Length);
        string value = TryDecompressToString(compressed, Encoding.Latin1);
        AddIfNotEmpty(dict, keyword, value);
    }

    private static void Extract_iTXt(Dictionary<string, string> dict, byte[] data, int offset, int length)
    {
        int cursor = offset;
        int end = offset + length;

        int sepKeyword = IndexOfByte(data, cursor, end - cursor, 0x00);
        if (sepKeyword < 0) return;
        string keyword = Encoding.UTF8.GetString(data, cursor, sepKeyword - cursor);
        cursor = sepKeyword + 1;
        if (cursor + 2 > end) return;

        byte compressionFlag = data[cursor++];
        byte compressionMethod = data[cursor++];

        int sepLang = IndexOfByte(data, cursor, end - cursor, 0x00);
        if (sepLang < 0) return;
        cursor = sepLang + 1;

        int sepTranslated = IndexOfByte(data, cursor, end - cursor, 0x00);
        if (sepTranslated < 0) return;
        cursor = sepTranslated + 1;

        if (cursor > end) return;
        int textLen = end - cursor;
        if (textLen < 0) return;

        string value;
        if (compressionFlag == 1)
        {
            var compressed = new byte[textLen];
            Buffer.BlockCopy(data, cursor, compressed, 0, textLen);
            value = TryDecompressToString(compressed, Encoding.UTF8);
        }
        else
        {
            value = Encoding.UTF8.GetString(data, cursor, textLen);
        }

        AddIfNotEmpty(dict, keyword, value);
    }

    private static int IndexOfByte(byte[] data, int start, int count, byte value)
    {
        int end = start + count;
        for (int i = start; i < end; i++)
        {
            if (data[i] == value) return i;
        }
        return -1;
    }

    private static void AddIfNotEmpty(Dictionary<string, string> dict, string keyword, string value)
    {
        if (string.IsNullOrEmpty(keyword)) return;
        if (string.IsNullOrEmpty(value)) return;
        if (!dict.ContainsKey(keyword)) dict[keyword] = value;
    }

    private static string TryDecompressToString(byte[] compressed, Encoding encoding)
    {
        if (TryDecompress(compressed, encoding, out var text)) return text;
        if (compressed.Length > 2)
        {
            var sliced = new byte[compressed.Length - 2];
            Buffer.BlockCopy(compressed, 2, sliced, 0, sliced.Length);
            if (TryDecompress(sliced, encoding, out text)) return text;
        }
        return string.Empty;
    }

    private static bool TryDecompress(byte[] data, Encoding encoding, out string text)
    {
        try
        {
            using (var ms = new MemoryStream(data))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress, leaveOpen: false))
            using (var sr = new StreamReader(ds, encoding, detectEncodingFromByteOrderMarks: false))
            {
                text = sr.ReadToEnd();
                return true;
            }
        }
        catch
        {
            text = string.Empty;
            return false;
        }
    }

    // ---------- JSON extraction helpers ----------
    private static string? TryExtractAfterMarker(string text, string marker)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var payload = text.Substring(idx + marker.Length);
        return TryExtractLargestJsonString(payload);
    }

    private static string? TryExtractLargestJsonString(string input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        int start = input.IndexOf('{');
        int end = input.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        string slice = input.Substring(start, end - start + 1);
        // Repair trailing commas
        slice = Regex.Replace(slice, ",\\s*([}\\]])", "$1");

        // Validate it parses to JSON
        try
        {
            var _ = JToken.Parse(slice);
            return slice;
        }
        catch
        {
            // Try trimming the last line
            var lines = slice.Split('\n');
            if (lines.Length > 1)
            {
                Array.Resize(ref lines, lines.Length - 1);
                var retry = string.Join("\n", lines);
                try { var _ = JToken.Parse(retry); return retry; } catch { }
            }
        }
        return null;
    }

    private static string BuildTextDiagnostics(Dictionary<string, string> texts, int maxPreview)
    {
        if (texts == null || texts.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (var kv in texts)
        {
            var value = kv.Value ?? string.Empty;
            var oneLine = value.Replace('\r', ' ').Replace('\n', ' ');
            if (oneLine.Length > maxPreview) oneLine = oneLine.Substring(0, maxPreview) + "…";
            sb.Append(kv.Key).Append(" (len=").Append(value.Length).Append("): ").Append(oneLine).AppendLine();
        }
        return sb.ToString();
    }

    private static JObject TryParseObject(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new JObject();
        try
        {
            var token = JToken.Parse(s);
            return token as JObject ?? new JObject();
        }
        catch
        {
            return new JObject();
        }
    }

    private static bool TryClassifyJson(string text, out string json, out bool isWorkflow)
    {
        json = string.Empty;
        isWorkflow = false;
        if (string.IsNullOrEmpty(text)) return false;

        // Try direct as-is first
        if (TryParseAndClassify(text, out json, out isWorkflow)) return true;

        // Try largest JSON slice in the text
        var slice = TryExtractLargestJsonString(text);
        if (!string.IsNullOrEmpty(slice))
        {
            if (TryParseAndClassify(slice!, out json, out isWorkflow)) return true;
        }
        return false;
    }

    private static bool TryParseAndClassify(string candidate, out string json, out bool isWorkflow)
    {
        json = string.Empty;
        isWorkflow = false;
        try
        {
            var token = JToken.Parse(candidate);
            if (token is JObject obj)
            {
                // Heuristic: workflow often has a top-level "nodes" array
                var nodes = obj["nodes"] as JArray;
                if (nodes != null)
                {
                    json = candidate;
                    isWorkflow = true;
                    return true;
                }

                // Heuristic: prompt is a map of id->node with class_type/inputs objects
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is JObject nodeObj)
                    {
                        if (nodeObj.ContainsKey("class_type") || nodeObj.ContainsKey("inputs"))
                        {
                            json = candidate;
                            isWorkflow = false;
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }
        return false;
    }

    // ---------- Chunk diagnostics ----------
    private static string GetChunkTypeSummary(byte[] data)
    {
        try
        {
            if (data == null || data.Length < 8) return "<none>";
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
            {
                return "<not a PNG>";
            }

            int offset = 8;
            int tEXt = 0, iTXt = 0, zTXt = 0, other = 0;
            while (offset + 8 <= data.Length)
            {
                uint len = ReadUInt32BigEndian(data, offset);
                offset += 4;
                if (offset + 4 > data.Length) break;
                string type = Encoding.ASCII.GetString(data, offset, 4);
                offset += 4;
                if (offset + len + 4 > data.Length) break;

                if (type == "tEXt") tEXt++;
                else if (type == "iTXt") iTXt++;
                else if (type == "zTXt") zTXt++;
                else other++;

                offset += (int)len + 4; // data + CRC
                if (type == "IEND") break;
            }
            return $"tEXt={tEXt}, iTXt={iTXt}, zTXt={zTXt}, other={other}";
        }
        catch
        {
            return "<scan failed>";
        }
    }

    // ---------- Generic scan for non-PNG images (JPEG, etc.) ----------
    private static void TryGenericScanForComfyJson(byte[] data, out string workflow, out string prompt, out string diagnostics)
    {
        workflow = string.Empty;
        prompt = string.Empty;
        diagnostics = string.Empty;
        try
        {
            // Look for markers 'workflow\0{' or 'prompt\0{' and also the largest {...}
            var text = Encoding.UTF8.GetString(data);
            var diags = new StringBuilder();

            var w = TryExtractAfterMarker(text, "workflow\0");
            if (!string.IsNullOrWhiteSpace(w))
            {
                workflow = w;
                diags.AppendLine("Found 'workflow\\0' marker in generic scan.");
            }

            var p = TryExtractAfterMarker(text, "prompt\0");
            if (!string.IsNullOrWhiteSpace(p))
            {
                prompt = p;
                diags.AppendLine("Found 'prompt\\0' marker in generic scan.");
            }

            if (string.IsNullOrWhiteSpace(workflow) && string.IsNullOrWhiteSpace(prompt))
            {
                var slice = TryExtractLargestJsonString(text);
                if (!string.IsNullOrEmpty(slice) && TryParseAndClassify(slice!, out var json, out var isWorkflow))
                {
                    if (isWorkflow) workflow = json; else prompt = json;
                    diags.AppendLine("Found generic JSON slice and classified.");
                }
            }

            // Show a short window around the first marker for debugging
            int idx = text.IndexOf("workflow\0", StringComparison.Ordinal);
            if (idx < 0) idx = text.IndexOf("prompt\0", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = Math.Max(0, idx - 24);
                int len = Math.Min(120, text.Length - start);
                var preview = text.Substring(start, len).Replace('\n', ' ').Replace('\r', ' ');
                diags.Append("Context preview: ").Append(preview).AppendLine();
            }

            diagnostics = diags.ToString();
        }
        catch (Exception ex)
        {
            diagnostics = "Generic scan failed: " + ex.Message;
        }
    }
}


