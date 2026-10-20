/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2026 DUONG DIEU PHAP
Project homepage: https://github.com/d2phap/ExifGlass

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

// Writes source/tests/fixtures/ai. Run from source/tools: dotnet run gen-ai-fixtures.cs [-- --check | --c2patool <exe>]
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

var toolsDir = Path.GetDirectoryName(ThisFile())!;
var sourceDir = Path.GetFullPath(Path.Combine(toolsDir, ".."));
var outDir = Path.Combine(sourceDir, "tests", "fixtures", "ai");
var configPath = Path.Combine(sourceDir, "__assets", "exiftool", "exifglass.config");
var exifToolPath = Path.Combine(sourceDir, "__assets", "exiftool",
    OperatingSystem.IsWindows() ? Path.Combine("win", "exiftool.exe") : Path.Combine("unix", "exiftool"));
string? c2paToolPath = null;
var check = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--check": check = true; break;
        case "--c2patool": c2paToolPath = Path.GetFullPath(args[++i]); break;
        case "--exiftool": exifToolPath = Path.GetFullPath(args[++i]); break;
        case "--out": outDir = Path.GetFullPath(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (!File.Exists(exifToolPath))
{
    Console.Error.WriteLine($"ExifTool not found: {exifToolPath} (pass --exiftool <path>)");
    return 2;
}

if (check)
{
    var tempDir = Path.Combine(Path.GetTempPath(), "eg-ai-fixtures-" + Environment.ProcessId);
    Directory.CreateDirectory(tempDir);
    try
    {
        var gen = new FixtureWriter(tempDir, exifToolPath, configPath, null, check: true);
        gen.WriteAll();
        return gen.CompareWith(outDir) ? 0 : 1;
    }
    finally
    {
        Directory.Delete(tempDir, true);
    }
}

Directory.CreateDirectory(outDir);
var writer = new FixtureWriter(outDir, exifToolPath, configPath, c2paToolPath);
writer.WriteAll();
Console.WriteLine($"{writer.Count} fixtures in {outDir}");
return 0;


static string ThisFile([CallerFilePath] string path = "") => path;


/// <summary>
/// Writes every fixture, one method per generator family, and the README that lists them.
/// </summary>
sealed class FixtureWriter(string outDir, string exifTool, string config, string? c2paTool, bool check = false)
{
    #region Fields and constants

    private readonly List<(string Section, string File, string Mimics)> _catalog = [];
    private readonly HashSet<string> _signed = new(StringComparer.Ordinal);
    private string _section = "";

    // a checkout of the Perl script may have lost its executable bit, so scripts run through perl
    private readonly bool _exifToolIsScript = IsScript(exifTool);

    private const string DST = "http://cv.iptc.org/newscodes/digitalsourcetype/";

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the number of fixtures written or kept.
    /// </summary>
    public int Count => _catalog.Count;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Writes all fixtures and the README.
    /// </summary>
    public void WriteAll()
    {
        WriteA1111Family();
        WriteComfyUi();
        WriteInvokeAi();
        WriteNovelAi();
        WriteFooocus();
        WriteOthers();
        WriteStandards();
        WriteC2paSynthetic();
        WriteC2paSigned();
        WriteNegatives();
        WriteReadme();
    }


    /// <summary>
    /// Compares the files written here with another folder; signed C2PA files are skipped.
    /// </summary>
    public bool CompareWith(string committedDir)
    {
        var same = true;
        foreach (var (_, file, _) in _catalog)
        {
            if (_signed.Contains(file)) continue;

            var mine = Path.Combine(outDir, file);
            var theirs = Path.Combine(committedDir, file);
            if (!File.Exists(theirs) || !File.ReadAllBytes(mine).AsSpan().SequenceEqual(File.ReadAllBytes(theirs)))
            {
                Console.WriteLine($"differs: {file}");
                same = false;
            }
        }

        Console.WriteLine(same ? $"{_catalog.Count - _signed.Count} fixtures match byte for byte" : "fixtures differ");
        return same;
    }

    #endregion // Public methods


    #region A1111 family

    private void WriteA1111Family()
    {
        _section = "A1111 family (A1111, Forge, SD.Next, Civitai, Krita AI Diffusion)";

        Save("a1111.png", "A1111 `images.py`: Pillow `PngInfo.add_text(\"parameters\")`, Latin-1 so tEXt; ADetailer 2nd unit, ControlNet, quoted hires prompt",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", Texts.A1111))));
        Save("a1111_utf8.png", "A1111 with non-Latin-1 text: Pillow falls back to iTXt",
            Png.Image(16, 16, Png.Before(Png.IText("parameters", Texts.A1111Utf8))));
        Save("a1111_mojibake.png", "a non-compliant writer that puts UTF-8 bytes in tEXt (ExifTool reads tEXt as cp1252: mojibake)",
            Png.Image(16, 16, Png.Before(Png.Chunk("tEXt", [.. "parameters\0"u8, .. Bytes.Utf8(Texts.Mojibake)]))));
        Save("a1111_ztxt.png", "zTXt (compressed Latin-1) `parameters` and compressed iTXt `workflow`",
            Png.Image(4, 4, Png.Before(Png.ZText("parameters", "zipped latin text\nSteps: 1, Sampler: x, Seed: 2"),
                Png.IText("workflow", "{\"k\": \"日本\"}", compressed: true))));
        Save("a1111_text_after_idat.png", "text chunks after IDAT (skipped by `-fast2`)",
            Png.Image(16, 16, after: [Png.Text("parameters", Texts.A1111), Png.Text("workflow", "{\"x\":1}")]));

        var piexif = Tiff.Build(bigEndian: true, ifd0: [],
            exif: [Tiff.UserCommentUnicode(Texts.A1111.Replace("masterpiece", "傑作", StringComparison.Ordinal), bigEndian: true)]);
        Save("a1111.jpg", "A1111 JPEG: piexif `UserComment` UNICODE + UTF-16BE in a big-endian TIFF; `piexif.insert` replaces APP0",
            Jpeg.ReplaceApp0(Jpeg.Exif(piexif)));
        Save("a1111.webp", "A1111 WebP: the piexif bytes through Pillow `exif=`, which drops the `Exif\\0\\0` header of a still WebP",
            Webp.WithExif(Webp.SimpleVp8(), piexif));
        Save("a1111.gif", "A1111 GIF: Pillow `comment=` (UTF-8 comment extension)",
            Gif.WithComment(Bytes.Utf8(Texts.A1111)));
        Save("a1111_sidecar.png", "A1111 `save_txt`: no metadata in the image, the infotext in `a1111_sidecar.txt`",
            Png.Image(16, 16));
        Save("a1111_sidecar.txt", "the sidecar text (LF line endings)", Bytes.Utf8(Texts.A1111));

        Save("forge.png", "Forge (Flux): `Distilled CFG Scale`, `Module N`, `Diffusion in Low Bits`, `Version: f2.0.1v1.10.1-previous-669-g...`",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", Texts.Forge))));
        Save("sdnext.png", "SD.Next `save.py`: `App: SD.Next`, `Version` (8-hex git hash), `Pipeline`, `Operations`, a `Template:` line",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", Texts.SdNext))));

        var sdnextExif = Tiff.Build(true, [], [Tiff.UserCommentUnicode(Texts.SdNext, true)]);
        Save("sdnext.jxl", "SD.Next JXL through pillow-jxl 1.3.7: raw TIFF in the `Exif` box, without the 4-byte offset the spec asks for",
            Jxl.WithExif(sdnextExif, withOffset: false));
        Save("sdnext_spec.jxl", "the same EXIF in a spec-compliant JXL `Exif` box (4-byte TIFF header offset first)",
            Jxl.WithExif(sdnextExif, withOffset: true));

        var civitai = Tiff.Build(bigEndian: false,
            ifd0: [Tiff.Ascii(0x013B, "ai"), Tiff.Ascii(0x0131, "4c6f9a1e-0b4d-4a43-9f1d-2b6f8e7c5a10")],
            exif: [Tiff.UserCommentUnicode(Texts.CivitaiGenerator, bigEndian: true)]);
        Save("civitai.jpg", "Civitai on-site JPEG: UNICODE + UTF-16BE inside a little-endian TIFF, IFD0 `Artist: ai`, `Software: <GUID>`",
            Jpeg.AfterApp0(Jpeg.Exif(civitai)));
        Save("civitai.png", "Civitai on-site PNG: the same EXIF in an `eXIf` chunk, no text chunk",
            Png.Image(16, 16, Png.Before(Png.Chunk("eXIf", civitai))));
        Save("civitai_extension.png", "A1111 + Civitai extension `gen_hashing.py`: unquoted `Hashes: {...}` at the end",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", Texts.CivitaiExtension))));
        Save("krita.png", "Krita AI Diffusion `image.py`: uncompressed iTXt `parameters` right after IHDR, `Model hash: unknown`",
            Png.Image(16, 16, Png.Before(Png.IText("parameters", Texts.Krita))));
        Save("krita_cloud.png", "Krita AI Diffusion cloud mode: plus XMP `Iptc4xmpExt:DigitalSourceType` (trainedAlgorithmicMedia)",
            Png.Image(16, 16, Png.Before(Png.IText("parameters", Texts.Krita), Png.Xmp(Xmp.DigitalSourceType(DST + "trainedAlgorithmicMedia")))));
    }

    #endregion // A1111 family


    #region ComfyUI

    private void WriteComfyUi()
    {
        _section = "ComfyUI";

        var prompt = PyJson.Dumps(Payloads.ComfyPrompt());
        var workflow = PyJson.Dumps(Payloads.ComfyWorkflow());

        Save("comfyui.png", "`SaveImage` (`nodes.py`): tEXt `prompt` + `workflow`, `json.dumps` defaults (ASCII `\\uXXXX`, bare `NaN`)",
            Png.Image(16, 16, Png.Before(Png.Text("prompt", prompt), Png.Text("workflow", workflow))));
        Save("comfyui_flux_subgraph.png", "Flux on SamplerCustomAdvanced, prompt through a string node, `Infinity`; workflow schema 1.0 with a subgraph",
            Png.Image(16, 16, Png.Before(Png.Text("prompt", PyJson.Dumps(Payloads.ComfyFluxPrompt())),
                Png.Text("workflow", PyJson.Dumps(Payloads.ComfyFluxWorkflow())))));

        var exif = Tiff.Build(bigEndian: true,
            ifd0: [Tiff.Ascii(0x010F, "workflow:" + workflow), Tiff.Ascii(0x0110, "prompt:" + prompt)], exif: []);
        Save("comfyui_anim.webp", "`SaveAnimatedWEBP`: Pillow animated WebP, EXIF Make `workflow:` / Model `prompt:` (`Exif\\0\\0` kept)",
            Webp.AnimatedWithExif([.. "Exif\0\0"u8, .. exif]));
        Save("comfyui_anim.png", "`SaveAnimatedPNG`: APNG with `comf` chunks (`key\\0json`) after the frames",
            Png.Animated(16, 16, 3, [Png.Chunk("comf", Bytes.Latin1("prompt\0" + prompt)), Png.Chunk("comf", Bytes.Latin1("workflow\0" + workflow))]));
        Save("comfyui.avif", "`SaveImageAdvanced`-style AVIF through Pillow/libavif: an `Exif` item with Make `workflow:` / Model `prompt:`",
            Media.AvifWithExif(exif));
        Save("comfyui.mp4", "VideoHelperSuite / FFmpeg `-movflags use_metadata_tags`: mdta keys, moov after mdat",
            Media.Mp4([("prompt", prompt), ("workflow", workflow), ("encoder", "Lavf")]));
        Save("comfyui.webm", "VideoHelperSuite WebM: Matroska SimpleTags `PROMPT` / `WORKFLOW` (no frames)",
            Media.Webm([("PROMPT", prompt), ("WORKFLOW", workflow)]));
        Save("comfyui_audio.flac", "`SaveAudio` FLAC: Vorbis comments `prompt` / `workflow` (no frames)",
            Media.Flac([("prompt", prompt), ("workflow", workflow)]));
        Save("comfyui_audio.mp3", "`SaveAudio` MP3: ID3v2.4 TXXX `prompt` / `workflow` + silent frames",
            Media.Mp3([("prompt", prompt), ("workflow", workflow)]));
        Save("comfyui.exr", "EXR save nodes: string header attributes `prompt` / `workflow`",
            Media.Exr([("prompt", prompt), ("workflow", workflow)]));
        Save("comfyui_big_workflow.png", "a 2 MB UI workflow for the large-payload read and parse benchmarks",
            Png.Image(16, 16, Png.Before(Png.Text("prompt", prompt), Png.Text("workflow", PyJson.Dumps(Payloads.BigWorkflow(2_000_000))))));
    }

    #endregion // ComfyUI


    #region InvokeAI, NovelAI, Fooocus

    private void WriteInvokeAi()
    {
        _section = "InvokeAI";

        Save("invokeai.png", "InvokeAI >= 3 `image_files_disk.py`: tEXt `invokeai_metadata`, `invokeai_workflow`, `invokeai_graph`",
            Png.Image(16, 16, Png.Before(Png.Text("invokeai_metadata", PyJson.Dumps(Payloads.InvokeMetadata())),
                Png.Text("invokeai_workflow", PyJson.Dumps(Payloads.InvokeWorkflow())),
                Png.Text("invokeai_graph", PyJson.Dumps(Payloads.InvokeGraph())))));
        Save("invokeai_graph_only.png", "InvokeAI output that carries only `invokeai_graph` (no metadata)",
            Png.Image(16, 16, Png.Before(Png.Text("invokeai_graph", PyJson.Dumps(Payloads.InvokeGraph())))));
        Save("invokeai_legacy.png", "InvokeAI <= 2.3 `pngwriter.py`: tEXt `Dream` (command line) + `sd-metadata` (JSON)",
            Png.Image(16, 16, Png.Before(Png.Text("Dream", Texts.InvokeDream),
                Png.Text("sd-metadata", PyJson.Dumps(Payloads.InvokeLegacy())))));
    }


    private void WriteNovelAi()
    {
        _section = "NovelAI";

        var chunks = Payloads.NovelAiChunks();
        Save("novelai.png", "NovelAI: tEXt `Title`, `Description`, `Software`, `Source`, `Generation time`, `Comment` (JSON)",
            Png.Image(16, 16, Png.Before([.. chunks.Select(c => Png.Text(c.Key, (string)c.Value!))]), rgba: true));

        // stealth_pngcomp: alpha LSB, column-major, MSB first, 32-bit length in bits, gzip of the chunks minus Title
        var gz = Zlib.Gzip(Bytes.Utf8(PyJson.Dumps(J.From(chunks.Where(c => c.Key != "Title")))));
        byte[] bits = [.. "stealth_pngcomp"u8, .. Bytes.Be32((uint)(gz.Length * 8)), .. gz];
        const int W = 64, H = 128;
        var alpha = new byte[W * H];
        Array.Fill(alpha, (byte)0xFF);
        for (var i = 0; i < bits.Length * 8; i++)
        {
            var bit = (bits[i / 8] >> (7 - i % 8)) & 1;
            alpha[i % H * W + i / H] = (byte)(0xFE | bit);
        }

        Save("novelai_stealth_only.png", "NovelAI stealth data only (alpha LSB, gzip JSON; ExifTool cannot read it)",
            Png.Image(W, H, rgba: true, alpha: alpha));
    }


    private void WriteFooocus()
    {
        _section = "Fooocus family";

        var meta = Payloads.FooocusMeta();
        Save("fooocus.png", "Fooocus `meta_parser.py`: tEXt `parameters` (JSON, ASCII) + `fooocus_scheme`",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", PyJson.Dumps(meta)), Png.Text("fooocus_scheme", "fooocus"))));
        Save("fooocus_a1111.png", "Fooocus with the a1111 scheme: infotext with `Raw prompt`, `Version: Fooocus v...`",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", Texts.FooocusA1111), Png.Text("fooocus_scheme", "a1111"))));

        // Pillow writes str values as ASCII-type entries in a big-endian IFD0, UserComment with no charset header
        var exif = Tiff.Build(bigEndian: true, ifd0: [
            Tiff.Ascii(0x0131, "Fooocus v2.5.5"),
            Tiff.Ascii(0x927C, "fooocus"),
            Tiff.Ascii(0x9286, PyJson.Dumps(meta, ascii: false), Encoding.UTF8)], exif: []);
        Save("fooocus.jpg", "Fooocus JPEG: Pillow EXIF IFD0 `UserComment` (no charset header), `Software`, MakerNote = scheme",
            Jpeg.AfterApp0(Jpeg.Exif(exif)));
        Save("fooocus.webp", "Fooocus WebP: the same Pillow EXIF", Webp.WithExif(Webp.SimpleVp8(), exif));

        var mre = PyJson.Dumps(Payloads.FooocusMre(), ascii: false);
        Save("fooocus_mre.png", "Fooocus-MRE `private_logger.py`: tEXt `Comment` JSON (`ensure_ascii=False`)",
            Png.Image(16, 16, Png.Before(Png.Text("Comment", mre))));
        Save("fooocus_mre.jpg", "Fooocus-MRE JPEG: the same JSON in a COM segment",
            Jpeg.AfterApp0(Jpeg.Segment(0xFE, Bytes.Utf8(mre))));
        Save("ruinedfooocus.png", "RuinedFooocus `async_worker.py`: tEXt `parameters` flat JSON, `software: RuinedFooocus`",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", PyJson.Dumps(Payloads.RuinedFooocus())))));
    }

    #endregion // InvokeAI, NovelAI, Fooocus


    #region Other generators

    private void WriteOthers()
    {
        _section = "SwarmUI, Easy Diffusion, Draw Things, Midjourney, Tensor.art, character cards";

        var swarm = PyJson.Dumps(Payloads.Swarm(), indent: 2);
        Save("swarmui.png", "SwarmUI `ImageFile.cs`: tEXt `parameters`, indented ASCII-escaped `sui_image_params` JSON",
            Png.Image(16, 16, Png.Before(Png.Text("parameters", swarm))));
        Save("swarmui.jpg", "SwarmUI JPEG (ImageSharp): ExifIFD `UserComment` UNICODE + UTF-16LE in a little-endian TIFF",
            Jpeg.AfterApp0(Jpeg.Exif(Tiff.Build(false, [], [Tiff.UserCommentUnicode(swarm, bigEndian: false)]))));

        var ed = Payloads.EasyDiffusion();
        Save("easydiffusion.png", "Easy Diffusion `file_utils.py`: one tEXt per field (snake_case keys, `str()` values)",
            Png.Image(16, 16, Png.Before([.. ed.Select(c => Png.Text(c.Key, (string)c.Value!))])));
        Save("easydiffusion.jpg", "Easy Diffusion JPEG: `piexif.insert`, `UserComment` UNICODE JSON",
            Jpeg.ReplaceApp0(Jpeg.Exif(Tiff.Build(true, [], [Tiff.UserCommentUnicode(PyJson.Dumps(ed), true)]))));

        Save("drawthings.png", "Draw Things: XMP in iTXt, `exif:UserComment` JSON + `dc:description` + `xmp:CreatorTool`",
            Png.Image(16, 16, Png.Before(Png.Xmp(Xmp.DrawThings(PyJson.Dumps(Payloads.DrawThings()))))));
        Save("midjourney.png", "Midjourney web PNG: tEXt `Creation Time`, `Author`, `Description` (`--flags`, `Job ID:`) + XMP `DigImageGUID`, `https` DigitalSourceType",
            Png.Image(16, 16, Png.Before(Png.Text("Creation Time", "2025-10-05T10:20:30+00:00"), Png.Text("Author", "some_user"),
                Png.Text("Description", Texts.Midjourney), Png.Xmp(Xmp.Midjourney()))));
        Save("tensorart.png", "Tensor.art: tEXt `generation_data` JSON with a trailing NUL, plus a ComfyUI `prompt` with custom loader nodes",
            Png.Image(16, 16, Png.Before(Png.Text("generation_data", PyJson.Dumps(Payloads.TensorArt()) + "\0"),
                Png.Text("prompt", PyJson.Dumps(Payloads.TensorArtPrompt())))));
        Save("charactercard.png", "LLM character card: tEXt `chara` (base64 JSON, v2) + `ccv3` (v3)",
            Png.Image(16, 16, Png.Before(Png.Text("chara", Payloads.CharacterCard("chara_card_v2", "2.0")),
                Png.Text("ccv3", Payloads.CharacterCard("chara_card_v3", "3.0")))));
    }

    #endregion // Other generators


    #region Standards

    private void WriteStandards()
    {
        _section = "Standards (IPTC, GB 45438-2025 AIGC, AI-training signals)";

        SaveWithExifTool("iptc_ai_fields.jpg", "IPTC 2025.1: `DigitalSourceType` + `AISystemUsed`, `AISystemVersionUsed`, `AIPromptInformation`, `AIPromptWriterName`",
            Jpeg.Base(),
            "-XMP-iptcExt:DigitalSourceType=" + DST + "trainedAlgorithmicMedia",
            "-XMP-iptcExt:AISystemUsed=Example Image Model", "-XMP-iptcExt:AISystemVersionUsed=2.1",
            "-XMP-iptcExt:AIPromptInformation=a lighthouse at dusk, oil painting",
            "-XMP-iptcExt:AIPromptWriterName=Jane Prompter");
        SaveWithExifTool("meta_ai_like.jpg", "Meta AI: only XMP `DigitalSourceType` = trainedAlgorithmicMedia",
            Jpeg.Base(), "-XMP-iptcExt:DigitalSourceType=" + DST + "trainedAlgorithmicMedia");
        SaveWithExifTool("google_photos_ai_edit.jpg", "Google Photos AI edit: `XMP-photoshop:Credit` = Edited with Google AI + compositeWithTrainedAlgorithmicMedia",
            Jpeg.Base(), "-XMP-photoshop:Credit=Edited with Google AI",
            "-XMP-iptcExt:DigitalSourceType=" + DST + "compositeWithTrainedAlgorithmicMedia");
        SaveWithExifTool("ai_training_signals.jpg", "AI-training signals: `XMP-plus:DataMining` + Exif 3.1 `LearningOptOutIn`",
            Jpeg.Base(), "-XMP-plus:DataMining=Prohibited for AI/ML training", "-ExifIFD:LearningOptOutIn#=2 2 0 4 0");

        var label = Payloads.Aigc();
        Save("aigc.png", "GB 45438-2025 Annex B: tEXt `AIGC` with the wrapped JSON `{\"AIGC\": {...}}`",
            Png.Image(16, 16, Png.Before(Png.Text("AIGC", PyJson.Dumps(new J { ["AIGC"] = label })))));
        Save("aigc_xmp.jpg", "TC260-PG-20259A: XMP `TC260:AIGC` (namespace `http://www.tc260.org.cn/ns/AIGC/1.0/`) with the flat JSON",
            Jpeg.AfterApp0(Jpeg.Xmp(Xmp.Tc260(PyJson.Dumps(label)))));
        SaveWithExifTool("aigc.mp4", "TC260-PG-20257A: MP4 `moov/udta/meta` mdta key `AIGC` with the flat JSON (written with exifglass.config)",
            Media.Mp4([]), "-Keys:AIGC=" + PyJson.Dumps(label));
    }

    #endregion // Standards


    #region C2PA

    private void WriteC2paSynthetic()
    {
        _section = "C2PA, synthetic store (unsigned, filler hashes; exact v1 claim_generator string)";

        var store = C2paStore.Synthetic(Jpeg.Base());
        Save("c2pa_synthetic.jpg", "JPEG APP11 JUMBF: a v1 Firefly-like parent + an active `c2pa.claim.v2` manifest",
            Jpeg.WithApp11(store));
        Save("c2pa_synthetic.png", "PNG `caBX` chunk before IDAT", Png.Image(16, 16, Png.Before(Png.Chunk("caBX", store))));
        Save("c2pa_synthetic.webp", "WebP `C2PA` chunk after EXIF",
            Webp.AppendChunk(Webp.WithExif(Webp.SimpleVp8(), Tiff.Build(true, [Tiff.Ascii(0x0131, "Example AI Service")], [])), "C2PA", store));
        Save("c2pa_synthetic.avif", "AVIF with a top-level C2PA `uuid` box", [.. Media.Avif(), .. Bmff.C2paUuid(store)]);
        Save("c2pa_synthetic.mp4", "MP4 with a top-level C2PA `uuid` box", [.. Media.Mp4([]), .. Bmff.C2paUuid(store)]);
    }


    private void WriteC2paSigned()
    {
        _section = "C2PA, signed with c2patool 0.27.22 (c2pa-rs 0.90) and its test certificate; not reproducible byte for byte";

        var c2pa = c2paTool is null ? null : new C2paSigner(c2paTool);
        var temp = Path.Combine(Path.GetTempPath(), "eg-c2pa-" + Environment.ProcessId);
        string Temp(string name) => Path.Combine(temp, name);

        if (c2pa is not null)
        {
            Directory.CreateDirectory(temp);
            File.WriteAllBytes(Temp("base.png"), Png.Image(16, 16));
            File.WriteAllBytes(Temp("base.jpg"), Jpeg.Base());
            File.WriteAllBytes(Temp("base.webp"), Webp.SimpleVp8());
            File.WriteAllBytes(Temp("base.mp4"), Media.Mp4Video());
        }

        try
        {
            Signed("c2pa_chatgpt.png", "ChatGPT (2025): v2; ingredient `ChatGPT` with `c2pa.created` by `GPT-4o` + trainedAlgorithmicMedia and a bare `c2pa.converted`; active `ChatGPT` with `c2pa.opened`", path =>
            {
                c2pa!.Sign(Temp("base.png"), Temp("chatgpt_step.png"), Manifests.ChatGptIngredient());
                c2pa.Sign(Temp("chatgpt_step.png"), path, Manifests.ChatGptActive());
            });
            Signed("c2pa_openai_api.png", "OpenAI API (2026): v2, one manifest `OpenAI Media Service API`, `gpt-image` 2.0, converted, `c2pa.watermarked.unbound`",
                path => c2pa!.Sign(Temp("base.png"), path, Manifests.OpenAiApi()));
            Signed("c2pa_dalle3.webp", "DALL-E 3 in ChatGPT (2024): v1 claims; ingredient `OpenAI-API` with `c2pa.created` by `DALL·E`; active `ChatGPT`", path =>
            {
                c2pa!.Sign(Temp("base.webp"), Temp("dalle_step.webp"), Manifests.DalleIngredient());
                c2pa.Sign(Temp("dalle_step.webp"), path, Manifests.DalleActive());
            });
            Signed("c2pa_firefly.jpg", "Adobe Firefly (2025): v1 claim `Adobe_Firefly`, `c2pa.created` by `Adobe Firefly` (string), firefly parameters, trainedAlgorithmicMedia",
                path => c2pa!.Sign(Temp("base.jpg"), path, Manifests.Firefly()));
            Signed("c2pa_photoshop_genfill.jpg", "Photoshop Generative Fill (values inferred): v2 `Adobe Photoshop`, camera ingredient, `c2pa.edited` by `Adobe Firefly` with compositeWithTrainedAlgorithmicMedia", path =>
            {
                c2pa!.Sign(Temp("base.jpg"), Temp("capture.jpg"), Manifests.Capture());
                c2pa.Sign(Temp("capture.jpg"), path, Manifests.GenerativeFill());
            });
            Signed("c2pa_gemini.png", "Gemini app: v2; ingredient `Google C2PA Core Generator Library` (created + SynthID, trainedAlgorithmicMedia), active wrapper (opened, watermark, converted); XMP Credit `Made with Google AI` + DigitalSourceType", path =>
            {
                File.Copy(Temp("base.png"), Temp("gemini_xmp.png"), true);
                RunExifTool(Temp("gemini_xmp.png"), "-XMP-photoshop:Credit=Made with Google AI", "-XMP-iptcExt:DigitalSourceType=" + DST + "trainedAlgorithmicMedia");
                c2pa!.Sign(Temp("gemini_xmp.png"), Temp("gemini_step.png"), Manifests.GoogleGenerated());
                c2pa.Sign(Temp("gemini_step.png"), path, Manifests.GoogleWrapper());
            });
            Signed("c2pa_microsoft.png", "Microsoft Designer / Azure OpenAI: v1 `Microsoft Responsible AI Image Provenance`, agent `Azure OpenAI DALL-E`, description `AI Generated Image`, no DigitalSourceType",
                path => c2pa!.Sign(Temp("base.png"), path, Manifests.Microsoft()));
            Signed("c2pa_sora.mp4", "Sora MP4: v2, one manifest `Sora`, `c2pa.created` by `Sora` + trainedAlgorithmicMedia",
                path => c2pa!.Sign(Temp("base.mp4"), path, Manifests.Sora()));
            Signed("c2pa_capture.jpg", "not AI: a camera-like `c2pa.created` with digitalCapture (provenance only, must not be flagged as AI)",
                path => c2pa!.Sign(Temp("base.jpg"), path, Manifests.Capture()));
        }
        finally
        {
            if (c2pa is not null && Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    #endregion // C2PA


    #region Negatives

    private void WriteNegatives()
    {
        _section = "Not AI (no false positives)";

        SaveWithExifTool("negative_camera.jpg", "camera JPEG: Make/Model/Software, a UserComment, XMP DigitalSourceType digitalCapture",
            Jpeg.Base(), "-EXIF:Make=Canon", "-EXIF:Model=Canon EOS R5", "-EXIF:Software=Adobe Lightroom Classic 14.0",
            "-EXIF:UserComment=Holiday in Hoi An", "-XMP-xmp:CreatorTool=Adobe Lightroom Classic 14.0",
            "-XMP-iptcExt:DigitalSourceType=" + DST + "digitalCapture", "-XMP-dc:Description=a lantern street at night");
        Save("negative_text.png", "PNG with ordinary text chunks: `Software` GIMP, `Comment`, a `Description` that mentions prompts",
            Png.Image(16, 16, Png.Before(Png.Text("Software", "GIMP 2.10.38"), Png.Text("Comment", "Created with GIMP"),
                Png.Text("Description", "slides for a talk about prompt engineering"))));
    }

    #endregion // Negatives


    #region Protected and private methods

    private void Save(string file, string mimics, byte[] data)
    {
        File.WriteAllBytes(Path.Combine(outDir, file), data);
        _catalog.Add((_section, file, mimics));
    }


    private void SaveWithExifTool(string file, string mimics, byte[] baseData, params string[] tagArgs)
    {
        var path = Path.Combine(outDir, file);
        File.WriteAllBytes(path, baseData);
        RunExifTool(path, tagArgs);
        _catalog.Add((_section, file, mimics + " (ExifTool-written)"));
    }


    private void Signed(string file, string mimics, Action<string> sign)
    {
        var path = Path.Combine(outDir, file);
        _signed.Add(file);
        _catalog.Add((_section, file, mimics));

        if (c2paTool is null)
        {
            if (!check && !File.Exists(path)) Console.Error.WriteLine($"missing {file}: run with --c2patool <path> to sign it");
            return;
        }

        if (File.Exists(path)) File.Delete(path);
        sign(path);
    }


    private void RunExifTool(string path, params string[] tagArgs)
    {
        var psi = new ProcessStartInfo(_exifToolIsScript ? "perl" : exifTool) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in (string[])[.. _exifToolIsScript ? [exifTool] : Array.Empty<string>(), "-config", config, "-charset", "UTF8", "-overwrite_original", .. tagArgs, path])
        {
            psi.ArgumentList.Add(arg);
        }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 || stderr.Length > 0)
        {
            throw new InvalidOperationException($"ExifTool failed on {path}: {stdout}{stderr}");
        }
    }


    private static bool IsScript(string path)
    {
        using var fs = File.OpenRead(path);
        return fs.ReadByte() == '#' && fs.ReadByte() == '!';
    }


    private void WriteReadme()
    {
        var sb = new StringBuilder();
        sb.Append(Texts.Lf("""
            # AI metadata fixtures

            Synthetic files that copy how each AI generator writes its metadata: tiny images (16 x 16 solid colour), no model output and no third-party content. The AI corpus tests read them with the bundled ExifTool started with `source/__assets/exiftool/exifglass.config`.

            Made by `source/tools/gen-ai-fixtures.cs`, which also writes this README. From `source/tools`:

            ```
            dotnet run gen-ai-fixtures.cs                           # rewrite every fixture except the signed C2PA files
            dotnet run gen-ai-fixtures.cs -- --check                # regenerate into a temp folder and compare byte for byte
            dotnet run gen-ai-fixtures.cs -- --c2patool <c2patool>  # also re-sign the C2PA files (c2patool's sample/ folder next to it)
            ```

            Everything except the signed C2PA files is reproducible byte for byte on any OS: fixed ids and seeds, no clock, stored (uncompressed) deflate, and nothing time-based from ExifTool. The signed C2PA files carry random manifest ids and ECDSA signatures, so they are committed and change only when re-signed. They use c2patool's public test certificate (`sample/es256_certs.pem`, "C2PA Test Signing Cert", valid to 2030) and fail trust checks on purpose. The video, audio and EXR files hold metadata only, no frames. Bytes marked `~` change when re-signed.

            """));

        foreach (var group in _catalog.GroupBy(c => c.Section))
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n## {group.Key}\n\n| File | Bytes | Mimics |\n|---|---:|---|\n");
            foreach (var (_, file, mimics) in group)
            {
                var path = Path.Combine(outDir, file);
                var size = File.Exists(path) ? new FileInfo(path).Length.ToString("N0", CultureInfo.InvariantCulture) : "missing";
                if (_signed.Contains(file)) size = "~" + size;
                sb.Append(CultureInfo.InvariantCulture, $"| `{file}` | {size} | {mimics} |\n");
            }
        }

        File.WriteAllText(Path.Combine(outDir, "README.md"), sb.ToString());
    }

    #endregion // Protected and private methods
}
/// <summary>
/// Big- and little-endian integers and small byte helpers.
/// </summary>
static class Bytes
{
    #region Public methods

    public static byte[] Be16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)v); return b; }

    public static byte[] Be32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }

    public static byte[] Be64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, v); return b; }

    public static byte[] Le16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)v); return b; }

    public static byte[] Le32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

    public static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    public static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    public static byte[] Latin1(string s) => Encoding.Latin1.GetBytes(s);

    public static byte[] Concat(IEnumerable<byte[]> parts) => [.. parts.SelectMany(p => p)];

    #endregion // Public methods
}


/// <summary>
/// CRC-32 (PNG, gzip) and Adler-32 (zlib).
/// </summary>
static class Checksum
{
    #region Fields and constants

    private static readonly uint[] _table = MakeTable();

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Computes the CRC-32 of the data.
    /// </summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }


    /// <summary>
    /// Computes the Adler-32 of the data.
    /// </summary>
    public static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    #endregion // Public methods


    #region Protected and private methods

    private static uint[] MakeTable()
    {
        var t = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }

        return t;
    }

    #endregion // Protected and private methods
}


/// <summary>
/// Zlib and gzip streams made of stored deflate blocks, so the bytes never depend on a compressor version.
/// </summary>
static class Zlib
{
    #region Public methods

    /// <summary>
    /// Wraps the data in a zlib stream.
    /// </summary>
    public static byte[] Stored(byte[] data) => [0x78, 0x01, .. Deflate(data), .. Bytes.Be32(Checksum.Adler32(data))];


    /// <summary>
    /// Wraps the data in a gzip stream with no time stamp.
    /// </summary>
    public static byte[] Gzip(byte[] data) =>
        [0x1F, 0x8B, 8, 0, 0, 0, 0, 0, 0, 0xFF, .. Deflate(data), .. Bytes.Le32(Checksum.Crc32(data)), .. Bytes.Le32((uint)data.Length)];

    #endregion // Public methods


    #region Protected and private methods

    private static byte[] Deflate(byte[] data)
    {
        var ms = new MemoryStream();
        var pos = 0;
        do
        {
            var len = Math.Min(65535, data.Length - pos);
            ms.WriteByte(pos + len >= data.Length ? (byte)1 : (byte)0);
            ms.Write(Bytes.Le16(len));
            ms.Write(Bytes.Le16(~len & 0xFFFF));
            ms.Write(data, pos, len);
            pos += len;
        }
        while (pos < data.Length);

        return ms.ToArray();
    }

    #endregion // Protected and private methods
}


/// <summary>
/// PNG and APNG files with text chunks placed like Pillow places them.
/// </summary>
static class Png
{
    #region Fields and constants

    private static readonly byte[] SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    #endregion // Fields and constants


    #region Public methods

    public static byte[][] Before(params byte[][] chunks) => chunks;


    /// <summary>
    /// Builds a chunk with its length and CRC.
    /// </summary>
    public static byte[] Chunk(string type, byte[] data)
    {
        byte[] body = [.. Bytes.Ascii(type), .. data];
        return [.. Bytes.Be32((uint)data.Length), .. body, .. Bytes.Be32(Checksum.Crc32(body))];
    }


    public static byte[] Text(string key, string value) => Chunk("tEXt", [.. Bytes.Latin1(key), 0, .. Bytes.Latin1(value)]);

    public static byte[] ZText(string key, string value) => Chunk("zTXt", [.. Bytes.Latin1(key), 0, 0, .. Zlib.Stored(Bytes.Latin1(value))]);

    public static byte[] IText(string key, string value, bool compressed = false) =>
        Chunk("iTXt", [.. Bytes.Latin1(key), 0, compressed ? (byte)1 : (byte)0, 0, 0, 0,
            .. compressed ? Zlib.Stored(Bytes.Utf8(value)) : Bytes.Utf8(value)]);

    public static byte[] Xmp(string packet) => IText("XML:com.adobe.xmp", packet);


    /// <summary>
    /// Builds a still PNG; the alpha array (one byte per pixel) is used when rgba is set.
    /// </summary>
    public static byte[] Image(int w, int h, byte[][]? before = null, byte[][]? after = null, bool rgba = false, byte[]? alpha = null) =>
        Bytes.Concat([SIGNATURE, Header(w, h, rgba), .. before ?? [], Chunk("IDAT", Zlib.Stored(Pixels(w, h, rgba, alpha, (40, 90, 160)))),
            .. after ?? [], Chunk("IEND", [])]);


    /// <summary>
    /// Builds an APNG as Pillow writes one: acTL, fcTL + IDAT, fcTL + fdAT per frame, then the after-IDAT chunks.
    /// </summary>
    public static byte[] Animated(int w, int h, int frames, byte[][] after)
    {
        var parts = new List<byte[]> { SIGNATURE, Header(w, h, false), Chunk("acTL", [.. Bytes.Be32((uint)frames), .. Bytes.Be32(0)]) };
        var seq = 0u;
        for (var f = 0; f < frames; f++)
        {
            parts.Add(Chunk("fcTL", [.. Bytes.Be32(seq++), .. Bytes.Be32((uint)w), .. Bytes.Be32((uint)h), .. Bytes.Be32(0), .. Bytes.Be32(0),
                .. Bytes.Be16(100), .. Bytes.Be16(1000), 0, 0]));
            var data = Zlib.Stored(Pixels(w, h, false, null, ((byte)(f * 60), 50, 50)));
            parts.Add(f == 0 ? Chunk("IDAT", data) : Chunk("fdAT", [.. Bytes.Be32(seq++), .. data]));
        }

        parts.AddRange(after);
        parts.Add(Chunk("IEND", []));
        return Bytes.Concat(parts);
    }

    #endregion // Public methods


    #region Protected and private methods

    private static byte[] Header(int w, int h, bool rgba) =>
        Chunk("IHDR", [.. Bytes.Be32((uint)w), .. Bytes.Be32((uint)h), 8, rgba ? (byte)6 : (byte)2, 0, 0, 0]);


    private static byte[] Pixels(int w, int h, bool rgba, byte[]? alpha, (byte R, byte G, byte B) c)
    {
        var bpp = rgba ? 4 : 3;
        var raw = new byte[h * (1 + w * bpp)];
        for (var y = 0; y < h; y++)
        {
            var row = y * (1 + w * bpp);
            for (var x = 0; x < w; x++)
            {
                var p = row + 1 + x * bpp;
                (raw[p], raw[p + 1], raw[p + 2]) = (c.R, c.G, c.B);
                if (rgba) raw[p + 3] = alpha?[y * w + x] ?? 0xFF;
            }
        }

        return raw;
    }

    #endregion // Protected and private methods
}


/// <summary>
/// JPEG files from one fixed 16 x 16 baseline image (encoded once with Pillow 12.3, quality 90).
/// </summary>
static class Jpeg
{
    #region Fields and constants

    private const string BASE = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAQABADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDweiiiv3M/LD//2Q==";

    #endregion // Fields and constants


    #region Public methods

    public static byte[] Base() => Convert.FromBase64String(BASE);

    public static byte[] Segment(byte marker, byte[] payload) => [0xFF, marker, .. Bytes.Be16(payload.Length + 2), .. payload];

    public static byte[] Exif(byte[] tiff) => Segment(0xE1, [.. "Exif\0\0"u8, .. tiff]);

    public static byte[] Xmp(string packet) => Segment(0xE1, [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. Bytes.Utf8(packet)]);


    /// <summary>
    /// Inserts segments after the JFIF APP0, as Pillow does.
    /// </summary>
    public static byte[] AfterApp0(params byte[][] segments)
    {
        var b = Base();
        var end = 4 + BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(4));
        return Bytes.Concat([b[..end], .. segments, b[end..]]);
    }


    /// <summary>
    /// Replaces the JFIF APP0 with the segment, as piexif.insert does.
    /// </summary>
    public static byte[] ReplaceApp0(byte[] segment)
    {
        var b = Base();
        var end = 4 + BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(4));
        return Bytes.Concat([b[..2], segment, b[end..]]);
    }


    /// <summary>
    /// Embeds a JUMBF box in APP11 segments (JPEG XT: "JP", box instance 1, packet sequence from 1).
    /// </summary>
    public static byte[] WithApp11(byte[] jumbf)
    {
        const int MAX = 65533 - 8;
        var segments = new List<byte[]>();
        var header = jumbf[..8];
        var seq = 1u;
        for (var pos = 0; pos < jumbf.Length; seq++)
        {
            var take = Math.Min(MAX - (pos == 0 ? 0 : 8), jumbf.Length - pos);
            byte[] payload = [.. "JP"u8, 0, 1, .. Bytes.Be32(seq), .. pos == 0 ? [] : header, .. jumbf.AsSpan(pos, take)];
            segments.Add(Segment(0xEB, payload));
            pos += take;
        }

        return AfterApp0([.. segments]);
    }

    #endregion // Public methods
}


/// <summary>
/// One TIFF directory entry.
/// </summary>
readonly record struct TiffEntry(ushort Tag, ushort Type, uint Count, byte[] Value);


/// <summary>
/// Minimal TIFF/EXIF blocks: IFD0 plus an optional ExifIFD, values after both directories.
/// </summary>
static class Tiff
{
    #region Public methods

    public static TiffEntry Ascii(ushort tag, string text, Encoding? encoding = null)
    {
        byte[] b = [.. (encoding ?? Encoding.Latin1).GetBytes(text), 0];
        return new(tag, 2, (uint)b.Length, b);
    }


    public static TiffEntry Undefined(ushort tag, byte[] b) => new(tag, 7, (uint)b.Length, b);


    /// <summary>
    /// UserComment with the UNICODE header, as piexif and ImageSharp write it.
    /// </summary>
    public static TiffEntry UserCommentUnicode(string text, bool bigEndian) =>
        Undefined(0x9286, [.. "UNICODE\0"u8, .. (bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode).GetBytes(text)]);


    /// <summary>
    /// Builds the TIFF block (no "Exif\0\0" header).
    /// </summary>
    public static byte[] Build(bool bigEndian, TiffEntry[] ifd0, TiffEntry[] exif)
    {
        var dir0 = ifd0.ToList();
        if (exif.Length > 0) dir0.Add(new(0x8769, 4, 1, new byte[4]));
        dir0.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        var dirExif = exif.OrderBy(e => e.Tag).ToList();

        var size0 = 2 + 12 * dir0.Count + 4;
        var sizeExif = dirExif.Count > 0 ? 2 + 12 * dirExif.Count + 4 : 0;
        var dataOffset = 8 + size0 + sizeExif;
        var data = new MemoryStream();

        byte[] U16(int v) => bigEndian ? Bytes.Be16(v) : Bytes.Le16(v);
        byte[] U32(uint v) => bigEndian ? Bytes.Be32(v) : Bytes.Le32(v);

        byte[] Pack(List<TiffEntry> entries)
        {
            var o = new MemoryStream();
            o.Write(U16(entries.Count));
            foreach (var e in entries)
            {
                var value = e.Tag == 0x8769 ? U32((uint)(8 + size0)) : e.Value;
                o.Write(U16(e.Tag));
                o.Write(U16(e.Type));
                o.Write(U32(e.Count));
                if (value.Length <= 4)
                {
                    o.Write(value);
                    o.Write(new byte[4 - value.Length]);
                }
                else
                {
                    o.Write(U32((uint)(dataOffset + data.Length)));
                    data.Write(value);
                    if (data.Length % 2 == 1) data.WriteByte(0);
                }
            }

            o.Write(U32(0));
            return o.ToArray();
        }

        var b0 = Pack(dir0);
        var bExif = dirExif.Count > 0 ? Pack(dirExif) : [];
        byte[] head = bigEndian ? [0x4D, 0x4D, 0, 0x2A] : [0x49, 0x49, 0x2A, 0];
        return [.. head, .. U32(8), .. b0, .. bExif, .. data.ToArray()];
    }

    #endregion // Public methods
}


/// <summary>
/// WebP files from fixed Pillow 12.3 bitstreams (a 16 x 16 still and a 3-frame animation).
/// </summary>
static class Webp
{
    #region Fields and constants

    private const string STILL = "UklGRjwAAABXRUJQVlA4IDAAAAAQAgCdASoQABAAAMASJaACdLoB+AH4AAPIAP746n//dk8kIm/4x//rKo24V9ZQAAA=";
    private const string ANIMATED = "UklGRvIAAABXRUJQVlA4WAoAAAACAAAADwAADwAAQU5JTQYAAAAAAAAAAABBTk1GPgAAAAAAAAAAAA8AAA8AAGQAAAJWUDggJgAAADABAJ0BKhAAEAABQCYlkAADcAD++q4P8GW2xUf/1t+OfzXgYAAAQU5NRjYAAAAAAAAAAAAPAAAPAABkAAAAVlA4IB4AAABUAQCdASoQABAAAAAmJZwABGGAAP74fLL3u+yWQABBTk1GQgAAAAAAAAAAAA8AAA8AAGQAAABWUDggKgAAAFQBAJ0BKhAAEAAAACYloAAEYYAA/vS+7/16pa5Sb//8bx/cp/inx0AAAA==";

    #endregion // Fields and constants


    #region Public methods

    public static byte[] SimpleVp8() => Convert.FromBase64String(STILL);


    /// <summary>
    /// Turns a simple WebP into an extended one with an EXIF chunk after the image.
    /// </summary>
    public static byte[] WithExif(byte[] simple, byte[] exif)
    {
        byte[] vp8x = [0x08, 0, 0, 0, 15, 0, 0, 15, 0, 0];
        return Build([("VP8X", vp8x), .. Parse(simple), ("EXIF", exif)]);
    }


    /// <summary>
    /// Adds an EXIF chunk to the animation, keeping the payload as given.
    /// </summary>
    public static byte[] AnimatedWithExif(byte[] exif)
    {
        var chunks = Parse(Convert.FromBase64String(ANIMATED));
        chunks[0].Data[0] |= 0x08;
        return Build([.. chunks, ("EXIF", exif)]);
    }


    public static byte[] AppendChunk(byte[] riff, string type, byte[] data) => Build([.. Parse(riff), (type, data)]);

    #endregion // Public methods


    #region Protected and private methods

    private static List<(string Type, byte[] Data)> Parse(byte[] riff)
    {
        var list = new List<(string, byte[])>();
        for (var i = 12; i + 8 <= riff.Length;)
        {
            var type = Encoding.ASCII.GetString(riff, i, 4);
            var len = (int)BinaryPrimitives.ReadUInt32LittleEndian(riff.AsSpan(i + 4));
            list.Add((type, riff[(i + 8)..(i + 8 + len)]));
            i += 8 + len + (len & 1);
        }

        return list;
    }


    private static byte[] Build(List<(string Type, byte[] Data)> chunks)
    {
        var body = new MemoryStream();
        body.Write("WEBP"u8);
        foreach (var (type, data) in chunks)
        {
            body.Write(Bytes.Ascii(type));
            body.Write(Bytes.Le32((uint)data.Length));
            body.Write(data);
            if (data.Length % 2 == 1) body.WriteByte(0);
        }

        return [.. "RIFF"u8, .. Bytes.Le32((uint)body.Length), .. body.ToArray()];
    }

    #endregion // Protected and private methods
}


/// <summary>
/// GIF with a comment extension, from a fixed Pillow 12.3 16 x 16 image.
/// </summary>
static class Gif
{
    #region Fields and constants

    private const string BASE = "R0lGODdhEAAQAIAAAAAAAAAAACwAAAAAEAAQAEAIHQABCBxIsKDBgwgTKlzIsKHDhxAjSpxIsaLFgQEBADs=";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Inserts the comment between the global colour table and the image, in 255-byte sub-blocks.
    /// </summary>
    public static byte[] WithComment(byte[] comment)
    {
        var b = Convert.FromBase64String(BASE);
        var ext = new MemoryStream();
        ext.Write([0x21, 0xFE]);
        for (var pos = 0; pos < comment.Length; pos += 255)
        {
            var n = Math.Min(255, comment.Length - pos);
            ext.WriteByte((byte)n);
            ext.Write(comment, pos, n);
        }

        ext.WriteByte(0);
        return [.. "GIF89a"u8, .. b[6..19], .. ext.ToArray(), .. b[19..]];
    }

    #endregion // Public methods
}


/// <summary>
/// ISO base media (MP4, AVIF, JXL) boxes.
/// </summary>
static class Bmff
{
    #region Fields and constants

    private static readonly byte[] C2PA_UUID = Convert.FromHexString("d8fec3d61b0e483c92975828877ec481");

    #endregion // Fields and constants


    #region Public methods

    public static byte[] Box(string type, params byte[][] parts) => Box(Bytes.Ascii(type), parts);


    public static byte[] Box(byte[] type, params byte[][] parts)
    {
        var body = Bytes.Concat(parts);
        return [.. Bytes.Be32((uint)(8 + body.Length)), .. type, .. body];
    }


    public static byte[] FullBox(string type, params byte[][] parts) => Box(type, [new byte[4], .. parts]);


    /// <summary>
    /// Builds the top-level C2PA box: uuid, version/flags, purpose "manifest", offset 0, JUMBF.
    /// </summary>
    public static byte[] C2paUuid(byte[] jumbf) => Box("uuid", C2PA_UUID, new byte[4], [.. "manifest\0"u8], new byte[8], jumbf);

    #endregion // Public methods
}


/// <summary>
/// Metadata-only media containers laid out as FFmpeg, libavif and libjxl lay them out.
/// </summary>
static class Media
{
    #region Fields and constants

    private const string MP4_VIDEO = "AAAAHGZ0eXBpc29tAAACAGlzb21pc28ybXA0MQAAAAhmcmVlAAAAHG1kYXQAAAGzABAHAAABthBgsYX22DEE7wAAAwttb292AAAAbG12aGQAAAAAAAAAAAAAAAAAAAPoAAAAKAABAAABAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAACWnRyYWsAAABcdGtoZAAAAAMAAAAAAAAAAAAAAAEAAAAAAAAAKAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAEAAAAAAEAAAABAAAAAAACRlZHRzAAAAHGVsc3QAAAAAAAAAAQAAACgAAAAAAAEAAAAAAdJtZGlhAAAAIG1kaGQAAAAAAAAAAAAAAAAAADIAAAACAFXEAAAAAAAtaGRscgAAAAAAAAAAdmlkZQAAAAAAAAAAAAAAAFZpZGVvSGFuZGxlcgAAAAF9bWluZgAAABR2bWhkAAAAAQAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAAAAABAAABPXN0YmwAAADZc3RzZAAAAAAAAAABAAAAyW1wNHYAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAEAAQAEgAAABIAAAAAAAAAAEKTGF2YyBtcGVnNAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY//8AAABPZXNkcwAAAAADgICAPgABAASAgIAwIBEAAAAAAw1AAAAPoAWAgIAeAAABsAEAAAG1iRMAAAEAAAABIADEjYgAzQCEAhRjBoCAgAECAAAAEHBhc3AAAAABAAAAAQAAABRidHJ0AAAAAAADDUAAAA+gAAAAGHN0dHMAAAAAAAAAAQAAAAEAAAIAAAAAHHN0c2MAAAAAAAAAAQAAAAEAAAABAAAAAQAAABRzdHN6AAAAAAAAABQAAAABAAAAFHN0Y28AAAAAAAAAAQAAACwAAAA9dWR0YQAAADVtZXRhAAAAAAAAACFoZGxyAAAAAAAAAABtZGlyYXBwbAAAAAAAAAAAAAAAAAhpbHN0";
    private const string AVIF = "AAAAIGZ0eXBhdmlmAAAAAGF2aWZtaWYxbWlhZk1BMUIAAADrbWV0YQAAAAAAAAAhaGRscgAAAAAAAAAAcGljdAAAAAAAAAAAAAAAAAAAAAAOcGl0bQAAAAAAAQAAAB5pbG9jAAAAAEQAAAEAAQAAAAEAAAETAAAAKwAAAChpaW5mAAAAAAABAAAAGmluZmUCAAAAAAEAAGF2MDFDb2xvcgAAAABqaXBycAAAAEtpcGNvAAAAFGlzcGUAAAAAAAAAEAAAABAAAAAQcGl4aQAAAAADCAgIAAAADGF2MUOBAAwAAAAAE2NvbHJuY2x4AAEADQAGgAAAABdpcG1hAAAAAAAAAAEAAQQBAoMEAAAAM21kYXQSAAoJGAz/2iAhoNCAMhwTx4eGZQGGGH4QAAAAOtrX6figsBH26GX0Bk6A";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// A 16 x 16 AVIF from Pillow 12.3 (libavif), no metadata.
    /// </summary>
    public static byte[] Avif() => Convert.FromBase64String(AVIF);


    /// <summary>
    /// Rebuilds the AVIF with a second item of type Exif (4-byte TIFF header offset, then TIFF) linked by cdsc, as libavif writes it.
    /// </summary>
    public static byte[] AvifWithExif(byte[] tiff)
    {
        var top = Children(Avif(), 0);
        var ftyp = top.First(b => b.Type == "ftyp").Raw;
        var meta = Children(top.First(b => b.Type == "meta").Raw, 12);
        var av1 = top.First(b => b.Type == "mdat").Raw[8..];
        byte[] exif = [.. Bytes.Be32(0), .. tiff];

        byte[] MetaBox(uint first)
        {
            var iloc = Bmff.FullBox("iloc", [0x44, 0x00], Bytes.Be16(2),
                Bytes.Be16(1), Bytes.Be16(0), Bytes.Be16(1), Bytes.Be32(first), Bytes.Be32((uint)av1.Length),
                Bytes.Be16(2), Bytes.Be16(0), Bytes.Be16(1), Bytes.Be32(first + (uint)av1.Length), Bytes.Be32((uint)exif.Length));
            var iinf = Bmff.FullBox("iinf", Bytes.Be16(2),
                Bmff.Box("infe", [2, 0, 0, 0], Bytes.Be16(1), Bytes.Be16(0), Bytes.Ascii("av01Color\0")),
                Bmff.Box("infe", [2, 0, 0, 0], Bytes.Be16(2), Bytes.Be16(0), Bytes.Ascii("Exif\0")));
            var iref = Bmff.FullBox("iref", Bmff.Box("cdsc", Bytes.Be16(2), Bytes.Be16(1), Bytes.Be16(1)));
            return Bmff.FullBox("meta", meta.First(b => b.Type == "hdlr").Raw, meta.First(b => b.Type == "pitm").Raw,
                iloc, iinf, iref, meta.First(b => b.Type == "iprp").Raw);
        }

        var dataStart = (uint)(ftyp.Length + MetaBox(0).Length + 8);
        return [.. ftyp, .. MetaBox(dataStart), .. Bmff.Box("mdat", av1, exif)];
    }


    /// <summary>
    /// A 16 x 16 one-frame MPEG-4 Part 2 MP4 from FFmpeg 9.0 with -bitexact, for signing.
    /// </summary>
    public static byte[] Mp4Video() => Convert.FromBase64String(MP4_VIDEO);


    /// <summary>
    /// MP4 with mdat first and moov last (FFmpeg without faststart); keys go to moov/udta/meta as mdta items.
    /// </summary>
    public static byte[] Mp4((string Key, string Value)[] keys)
    {
        var ftyp = Bmff.Box("ftyp", Bytes.Ascii("isom"), Bytes.Be32(512), Bytes.Ascii("isomiso2avc1mp41"));
        var mdat = Bmff.Box("mdat", new byte[8]);
        var mvhd = Bmff.FullBox("mvhd", Bytes.Be32(0), Bytes.Be32(0), Bytes.Be32(1000), Bytes.Be32(200), Bytes.Be32(0x00010000),
            Bytes.Be16(0x0100), new byte[10], Matrix(), new byte[24], Bytes.Be32(2));

        if (keys.Length == 0) return [.. ftyp, .. mdat, .. Bmff.Box("moov", mvhd)];

        var hdlr = Bmff.FullBox("hdlr", Bytes.Be32(0), Bytes.Ascii("mdta"), new byte[12], [0]);
        var keyList = Bmff.FullBox("keys", [Bytes.Be32((uint)keys.Length), .. keys.Select(k => Bmff.Box("mdta", Bytes.Utf8(k.Key)))]);
        var ilst = Bmff.Box("ilst", [.. keys.Select((k, i) =>
            Bmff.Box(Bytes.Be32((uint)i + 1), Bmff.Box("data", Bytes.Be32(1), Bytes.Be32(0), Bytes.Utf8(k.Value))))]);
        var udta = Bmff.Box("udta", Bmff.FullBox("meta", hdlr, keyList, ilst));
        return [.. ftyp, .. mdat, .. Bmff.Box("moov", mvhd, udta)];
    }


    /// <summary>
    /// WebM (Matroska) with global SimpleTags; no clusters.
    /// </summary>
    public static byte[] Webm((string Name, string Value)[] tags)
    {
        var header = Ebml(0x1A45DFA3, Ebml(0x4286, Uint(1)), Ebml(0x42F7, Uint(1)), Ebml(0x42F2, Uint(4)), Ebml(0x42F3, Uint(8)),
            Ebml(0x4282, Bytes.Ascii("webm")), Ebml(0x4287, Uint(4)), Ebml(0x4285, Uint(2)));
        var info = Ebml(0x1549A966, Ebml(0x2AD7B1, Uint(1_000_000)), Ebml(0x4D80, Bytes.Ascii("Lavf")), Ebml(0x5741, Bytes.Ascii("Lavf")),
            Ebml(0x4489, Bytes.Be64((ulong)BitConverter.DoubleToInt64Bits(200.0))));
        var tracks = Ebml(0x1654AE6B, Ebml(0xAE, Ebml(0xD7, Uint(1)), Ebml(0x73C5, Uint(1)), Ebml(0x83, Uint(1)),
            Ebml(0x86, Bytes.Ascii("V_VP9")), Ebml(0xE0, Ebml(0xB0, Uint(16)), Ebml(0xBA, Uint(16)))));
        var simple = tags.Select(t => Ebml(0x67C8, Ebml(0x45A3, Bytes.Utf8(t.Name)), Ebml(0x4487, Bytes.Utf8(t.Value))));
        var tagsEl = Ebml(0x1254C367, Ebml(0x7373, [Ebml(0x63C0), .. simple]));
        return [.. header, .. Ebml(0x18538067, info, tracks, tagsEl)];
    }


    /// <summary>
    /// FLAC with STREAMINFO (no samples) and a Vorbis comment block.
    /// </summary>
    public static byte[] Flac((string Key, string Value)[] comments)
    {
        byte[] info = [.. Bytes.Be16(4096), .. Bytes.Be16(4096), 0, 0, 0, 0, 0, 0,
            .. Bytes.Be64((44100UL << 44) | (15UL << 36)), .. new byte[16]];
        var vc = new MemoryStream();
        vc.Write(Bytes.Le32(4));
        vc.Write("Lavf"u8);
        vc.Write(Bytes.Le32((uint)comments.Length));
        foreach (var (key, value) in comments)
        {
            var entry = Bytes.Utf8(key + "=" + value);
            vc.Write(Bytes.Le32((uint)entry.Length));
            vc.Write(entry);
        }

        return [.. "fLaC"u8, .. Block(0, info), .. Block(0x84, vc.ToArray())];

        static byte[] Block(byte type, byte[] data) => [type, (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length, .. data];
    }


    /// <summary>
    /// MP3 with an ID3v2.4 tag of TXXX frames (Latin-1 when ASCII, as FFmpeg writes), padding and three silent frames.
    /// </summary>
    public static byte[] Mp3((string Description, string Value)[] frames)
    {
        var body = new MemoryStream();
        foreach (var (desc, value) in frames)
        {
            var ascii = (desc + value).All(c => c < 0x80);
            byte[] content = [ascii ? (byte)0 : (byte)3, .. Bytes.Utf8(desc), 0, .. Bytes.Utf8(value)];
            body.Write("TXXX"u8);
            body.Write(SyncSafe(content.Length));
            body.Write([0, 0]);
            body.Write(content);
        }

        body.Write(new byte[10]);
        byte[] frame = [0xFF, 0xFB, 0x10, 0xC0, .. new byte[100]];
        return [.. "ID3"u8, 4, 0, 0, .. SyncSafe((int)body.Length), .. body.ToArray(), .. frame, .. frame, .. frame];
    }


    /// <summary>
    /// OpenEXR 1 x 1 float RGB scanline image with extra string attributes.
    /// </summary>
    public static byte[] Exr((string Name, string Value)[] strings)
    {
        var h = new MemoryStream();
        h.Write([0x76, 0x2F, 0x31, 0x01, 2, 0, 0, 0]);
        byte[] channels = [.. Channel("B"), .. Channel("G"), .. Channel("R"), 0];
        Attr(h, "channels", "chlist", channels);
        Attr(h, "compression", "compression", [0]);
        Attr(h, "dataWindow", "box2i", new byte[16]);
        Attr(h, "displayWindow", "box2i", new byte[16]);
        Attr(h, "lineOrder", "lineOrder", [0]);
        Attr(h, "pixelAspectRatio", "float", BitConverter.GetBytes(1f));
        foreach (var (name, value) in strings) Attr(h, name, "string", Bytes.Utf8(value));
        Attr(h, "screenWindowCenter", "v2f", new byte[8]);
        Attr(h, "screenWindowWidth", "float", BitConverter.GetBytes(1f));
        h.WriteByte(0);

        var blockStart = h.Length + 8;
        h.Write(BitConverter.GetBytes((ulong)blockStart));
        h.Write(BitConverter.GetBytes(0));
        h.Write(BitConverter.GetBytes(12));
        h.Write(new byte[12]);
        return h.ToArray();

        static byte[] Channel(string name) => [.. Bytes.Ascii(name), 0, .. BitConverter.GetBytes(2), 0, 0, 0, 0, .. BitConverter.GetBytes(1), .. BitConverter.GetBytes(1)];

        static void Attr(MemoryStream s, string name, string type, byte[] value)
        {
            s.Write(Bytes.Ascii(name + "\0" + type + "\0"));
            s.Write(BitConverter.GetBytes(value.Length));
            s.Write(value);
        }
    }

    #endregion // Public methods


    #region Protected and private methods

    private static List<(string Type, byte[] Raw)> Children(byte[] data, int start)
    {
        var list = new List<(string, byte[])>();
        for (var i = start; i + 8 <= data.Length;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i));
            list.Add((Encoding.ASCII.GetString(data, i + 4, 4), data[i..(i + size)]));
            i += size;
        }

        return list;
    }


    private static byte[] Matrix() => Bytes.Concat([Bytes.Be32(0x00010000), new byte[12], Bytes.Be32(0x00010000), new byte[12], Bytes.Be32(0x40000000)]);


    private static byte[] SyncSafe(int n) => [(byte)((n >> 21) & 0x7F), (byte)((n >> 14) & 0x7F), (byte)((n >> 7) & 0x7F), (byte)(n & 0x7F)];


    private static byte[] Uint(ulong v)
    {
        var b = Bytes.Be64(v);
        var skip = 0;
        while (skip < 7 && b[skip] == 0) skip++;
        return b[skip..];
    }


    private static byte[] Ebml(uint id, params byte[][] children)
    {
        var data = Bytes.Concat(children);
        var idBytes = Uint(id);
        var n = (ulong)data.Length;
        byte[] size = n < 0x7F ? [(byte)(0x80 | n)]
            : n < 0x3FFF ? Bytes.Be16((int)(0x4000 | n))
            : [0x01, .. Bytes.Be64(n)[1..]];
        return [.. idBytes, .. size, .. data];
    }

    #endregion // Protected and private methods
}


/// <summary>
/// JPEG XL container with an Exif box, from a fixed 16 x 16 codestream (FFmpeg 9.0 libjxl).
/// </summary>
static class Jxl
{
    #region Fields and constants

    private const string CODESTREAM = "/wpDBgATiAIA1AC1nyAAABUqo4wbvJzr+fJDh8W0jesMbbVtYQljs70wSEg4YnCbQJonit0aBqTjRWAoFKCSBA==";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Builds JXL signature, ftyp, Exif and jxlc boxes; the spec puts a 4-byte TIFF header offset before the TIFF.
    /// </summary>
    public static byte[] WithExif(byte[] tiff, bool withOffset) => Bytes.Concat([
        Bmff.Box("JXL ", [0x0D, 0x0A, 0x87, 0x0A]),
        Bmff.Box("ftyp", Bytes.Ascii("jxl "), Bytes.Be32(0), Bytes.Ascii("jxl ")),
        Bmff.Box("Exif", withOffset ? Bytes.Be32(0) : [], tiff),
        Bmff.Box("jxlc", Convert.FromBase64String(CODESTREAM))]);

    #endregion // Public methods
}


/// <summary>
/// XMP packets written by hand where the generator writes XMP itself.
/// </summary>
static class Xmp
{
    #region Public methods

    /// <summary>
    /// Draw Things: exif:UserComment JSON (XML-escaped), dc:description (prompt, -negative, parameters) and xmp:CreatorTool.
    /// </summary>
    public static string DrawThings(string json) => Packet(
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:exif=\"http://ns.adobe.com/exif/1.0/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\"",
        Alt("dc:description", "a cozy cabin, <snow>\n-blurry\nSteps: 4, Sampler: Euler A Trailing, Guidance Scale: 1.0, Seed: 99, Size: 1024x1024, Model: flux_1_schnell_q8p.ckpt, Strength: 1.0, Seed Mode: Scale Alike, Shift: 3.0")
        + Alt("exif:UserComment", json) + "<xmp:CreatorTool>Draw Things</xmp:CreatorTool>");


    /// <summary>
    /// Midjourney: IPTC Extension DigImageGUID (the job id) and an https DigitalSourceType URI.
    /// </summary>
    public static string Midjourney() => Packet(
        "xmlns:Iptc4xmpExt=\"http://iptc.org/std/Iptc4xmpExt/2008-02-29/\"",
        "<Iptc4xmpExt:DigImageGUID>0123abcd-4567-89ef-0123-456789abcdef</Iptc4xmpExt:DigImageGUID>"
        + "<Iptc4xmpExt:DigitalSourceType>https://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia</Iptc4xmpExt:DigitalSourceType>");


    /// <summary>
    /// Only an IPTC Extension DigitalSourceType.
    /// </summary>
    public static string DigitalSourceType(string uri) => Packet(
        "xmlns:Iptc4xmpExt=\"http://iptc.org/std/Iptc4xmpExt/2008-02-29/\"",
        "<Iptc4xmpExt:DigitalSourceType>" + uri + "</Iptc4xmpExt:DigitalSourceType>");


    /// <summary>
    /// The TC260 implicit label property with the flat JSON.
    /// </summary>
    public static string Tc260(string json) => Packet(
        "xmlns:TC260=\"http://www.tc260.org.cn/ns/AIGC/1.0/\"",
        "<TC260:AIGC>" + Escape(json) + "</TC260:AIGC>");

    #endregion // Public methods


    #region Protected and private methods

    private static string Packet(string namespaces, string body) =>
        "<?xpacket begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" " + namespaces + ">"
        + body + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";


    private static string Alt(string name, string value) =>
        $"<{name}><rdf:Alt><rdf:li xml:lang=\"x-default\">{Escape(value)}</rdf:li></rdf:Alt></{name}>";


    private static string Escape(string s) => s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    #endregion // Protected and private methods
}


/// <summary>
/// An ordered JSON object; the indexer setter appends, so object initializers keep their order.
/// </summary>
sealed class J : List<KeyValuePair<string, object?>>
{
    #region Properties

    public object? this[string key]
    {
        get => this.First(p => p.Key == key).Value;
        set => Add(new(key, value));
    }

    #endregion // Properties


    #region Public methods

    public static J From(IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        var j = new J();
        j.AddRange(pairs);
        return j;
    }

    #endregion // Public methods
}


/// <summary>
/// Writes JSON exactly as Python's json.dumps does (separators, ensure_ascii, bare NaN, float repr).
/// </summary>
static class PyJson
{
    #region Public methods

    public static string Dumps(object? value, bool ascii = true, int? indent = null)
    {
        var sb = new StringBuilder();
        Write(sb, value, ascii, indent, 0);
        return sb.ToString();
    }

    #endregion // Public methods


    #region Protected and private methods

    private static void Write(StringBuilder sb, object? v, bool ascii, int? indent, int level)
    {
        switch (v)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case string s: WriteString(sb, s, ascii); break;
            case int or long: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(Float(d)); break;
            case J obj:
                WriteItems(sb, '{', '}', obj, ascii, indent, level, (p, l) =>
                {
                    WriteString(sb, p.Key, ascii);
                    sb.Append(": ");
                    Write(sb, p.Value, ascii, indent, l);
                });
                break;
            case IEnumerable<object?> list:
                WriteItems(sb, '[', ']', list.ToList(), ascii, indent, level, (x, l) => Write(sb, x, ascii, indent, l));
                break;
            default: throw new ArgumentException($"Unsupported JSON value: {v.GetType()}");
        }
    }


    private static void WriteItems<T>(StringBuilder sb, char open, char close, IReadOnlyList<T> items, bool ascii, int? indent, int level, Action<T, int> item)
    {
        sb.Append(open);
        if (items.Count == 0)
        {
            sb.Append(close);
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (indent is { } n) sb.Append('\n').Append(' ', n * (level + 1));
            item(items[i], level + 1);
            if (i < items.Count - 1) sb.Append(indent is null ? ", " : ",");
        }

        if (indent is { } m) sb.Append('\n').Append(' ', m * level);
        sb.Append(close);
    }


    private static void WriteString(StringBuilder sb, string s, bool ascii)
    {
        sb.Append('"');
        foreach (var c in s)
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
                    if (c < 0x20 || (ascii && c > 0x7E && c != 0x7F)) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }


    private static string Float(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d > 0 ? "Infinity" : "-Infinity";

        var r = d.ToString("R", CultureInfo.InvariantCulture);
        if (r.Contains('E', StringComparison.Ordinal)) throw new ArgumentException($"Use a plain float: {r}");
        return r.Contains('.', StringComparison.Ordinal) ? r : r + ".0";
    }

    #endregion // Protected and private methods
}


/// <summary>
/// A CBOR tagged value.
/// </summary>
sealed record CborTag(ulong Tag, object? Value);


/// <summary>
/// A CBOR map with integer keys (COSE headers).
/// </summary>
sealed class CborIntMap : List<KeyValuePair<long, object?>>
{
    #region Properties

    public object? this[long key]
    {
        get => this.First(p => p.Key == key).Value;
        set => Add(new(key, value));
    }

    #endregion // Properties
}


/// <summary>
/// A hand-built C2PA manifest store (JUMBF + CBOR), unsigned: hashes and the signature are filler bytes.
/// </summary>
static class C2paStore
{
    #region Fields and constants

    private static readonly byte[] JUMBF_SUFFIX = Convert.FromHexString("00110010800000AA00389B71");
    private const string DST = "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Builds a store with a v1 Firefly-like parent manifest and an active v2 manifest.
    /// </summary>
    public static byte[] Synthetic(byte[] thumbnail)
    {
        var cert = (byte[])[0x30, 0x82, 0x01, 0x00, .. Enumerable.Repeat((byte)0x5A, 256)];
        var label = "urn:c2pa:00000000-0000-0000-0000-000000001234";
        var cose = new CborTag(18, new object?[]
        {
            Cbor(new CborIntMap { [1] = -37L, [33] = new object?[] { cert } }),
            new J { ["sigTst2"] = new J { ["tstTokens"] = new object?[] { new J { ["val"] = Fill(0x44, 16) } } }, ["pad"] = new byte[4] },
            null,
            Fill(0x55, 256),
        });

        var assertions = SuperBox("c2as", "c2pa.assertions",
            CborAssertion("c2pa.actions.v2", new J
            {
                ["actions"] = new object?[]
                {
                    new J
                    {
                        ["action"] = "c2pa.created", ["digitalSourceType"] = DST,
                        ["softwareAgent"] = new J { ["name"] = "Example Image Model", ["version"] = "2.0" },
                        ["description"] = "AI Generated Image",
                    },
                },
            }),
            CborAssertion("c2pa.hash.data", new J
            {
                ["exclusions"] = new object?[] { new J { ["start"] = 20L, ["length"] = 4000L } }, ["name"] = "jumbf manifest",
                ["alg"] = "sha256", ["hash"] = Fill(0x11, 32), ["pad"] = new byte[8],
            }),
            JsonAssertion("stds.schema-org.CreativeWork", new J
            {
                ["@context"] = "https://schema.org", ["@type"] = "CreativeWork",
                ["author"] = new object?[] { new J { ["@type"] = "Person", ["name"] = "Jane Prompter" } },
            }),
            EmbeddedFile("c2pa.thumbnail.claim.jpeg", "image/jpeg", thumbnail));

        var claimV2 = SuperBox("c2cl", "c2pa.claim.v2", Box("cbor", Cbor(new J
        {
            ["instanceID"] = "xmp:iid:00000000-0000-0000-0000-000000000099",
            ["claim_generator_info"] = new J { ["name"] = "Example AI Service", ["version"] = "1.2.3", ["org.contentauth.c2pa_rs"] = "0.67.1" },
            ["signature"] = "self#jumbf=/c2pa/" + label + "/c2pa.signature",
            ["created_assertions"] = new object?[]
            {
                new J { ["url"] = "self#jumbf=c2pa.assertions/c2pa.actions.v2", ["hash"] = Fill(0x22, 32) },
                new J { ["url"] = "self#jumbf=c2pa.assertions/c2pa.hash.data", ["hash"] = Fill(0x33, 32) },
            },
            ["dc:title"] = "image.jpg", ["alg"] = "sha256",
        })));
        var manifest = SuperBox("c2ma", label, assertions, claimV2, SuperBox("c2cs", "c2pa.signature", Box("cbor", Cbor(cose))));

        var label1 = "urn:uuid:00000000-0000-0000-0000-000000005678";
        var assertions1 = SuperBox("c2as", "c2pa.assertions", CborAssertion("c2pa.actions", new J
        {
            ["actions"] = new object?[] { new J { ["action"] = "c2pa.created", ["softwareAgent"] = "Adobe Firefly", ["digitalSourceType"] = DST } },
        }));
        var claimV1 = SuperBox("c2cl", "c2pa.claim", Box("cbor", Cbor(new J
        {
            ["claim_generator"] = "Adobe_Firefly adobe_c2pa/0.12.4+sven2 c2pa-rs/0.32.7",
            ["claim_generator_info"] = new object?[] { new J { ["name"] = "Adobe Firefly", ["version"] = "0.12.4" } },
            ["dc:title"] = "Generated image", ["dc:format"] = "image/jpeg",
            ["instanceID"] = "xmp:iid:00000000-0000-0000-0000-000000000077",
            ["signature"] = "self#jumbf=c2pa.signature",
            ["assertions"] = new object?[] { new J { ["url"] = "self#jumbf=c2pa.assertions/c2pa.actions", ["hash"] = Fill(0x66, 32) } },
            ["alg"] = "sha256",
        })));
        var manifest1 = SuperBox("c2ma", label1, assertions1, claimV1, SuperBox("c2cs", "c2pa.signature", Box("cbor", Cbor(cose))));

        return SuperBox("c2pa", "c2pa", manifest1, manifest);
    }

    #endregion // Public methods


    #region Protected and private methods

    private static byte[] Fill(byte b, int n) => Enumerable.Repeat(b, n).ToArray();

    private static byte[] Box(string type, params byte[][] parts) => Bmff.Box(type, parts);

    private static byte[] Jumd(string fourcc, string label) => Box("jumd", Bytes.Ascii(fourcc), JUMBF_SUFFIX, [0x03], Bytes.Utf8(label + "\0"));

    private static byte[] SuperBox(string fourcc, string label, params byte[][] children) => Box("jumb", [Jumd(fourcc, label), .. children]);

    private static byte[] CborAssertion(string label, J obj) => SuperBox("cbor", label, Box("cbor", Cbor(obj)));

    private static byte[] JsonAssertion(string label, J obj) => SuperBox("json", label, Box("json", Bytes.Utf8(PyJson.Dumps(obj))));


    private static byte[] EmbeddedFile(string label, string mime, byte[] data)
    {
        byte[] head = [.. Convert.FromHexString("40CB0C32BB8A489DA70B2AD6F47F4369"), 0x03, .. Bytes.Utf8(label + "\0")];
        return Box("jumb", Box("jumd", head), Box("bfdb", [0], Bytes.Utf8(mime + "\0")), Box("bidb", data));
    }


    private static byte[] Head(int major, ulong n) => n switch
    {
        < 24 => [(byte)((major << 5) | (int)n)],
        < 0x100 => [(byte)((major << 5) | 24), (byte)n],
        < 0x10000 => [(byte)((major << 5) | 25), .. Bytes.Be16((int)n)],
        < 0x100000000 => [(byte)((major << 5) | 26), .. Bytes.Be32((uint)n)],
        _ => [(byte)((major << 5) | 27), .. Bytes.Be64(n)],
    };


    private static byte[] Cbor(object? v) => v switch
    {
        null => [0xF6],
        true => [0xF5],
        false => [0xF4],
        CborTag t => [.. Head(6, t.Tag), .. Cbor(t.Value)],
        long n => n >= 0 ? Head(0, (ulong)n) : Head(1, (ulong)(-1 - n)),
        byte[] b => [.. Head(2, (ulong)b.Length), .. b],
        string s => [.. Head(3, (ulong)Encoding.UTF8.GetByteCount(s)), .. Bytes.Utf8(s)],
        J map => [.. Head(5, (ulong)map.Count), .. map.SelectMany(p => (byte[])[.. Cbor(p.Key), .. Cbor(p.Value)])],
        CborIntMap map => [.. Head(5, (ulong)map.Count), .. map.SelectMany(p => (byte[])[.. Cbor(p.Key), .. Cbor(p.Value)])],
        IEnumerable<object?> list => [.. Head(4, (ulong)list.Count()), .. list.SelectMany(Cbor)],
        _ => throw new ArgumentException($"Unsupported CBOR value: {v.GetType()}"),
    };

    #endregion // Protected and private methods
}


/// <summary>
/// Signs files with c2patool, its sample test certificate and no thumbnails or time stamps.
/// </summary>
sealed class C2paSigner(string exe)
{
    #region Public methods

    /// <summary>
    /// Adds the manifest to the input; an input that already has one becomes the parent ingredient.
    /// </summary>
    public void Sign(string input, string output, J manifest)
    {
        var sample = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exe))!, "sample");
        var definition = new J
        {
            ["alg"] = "es256",
            ["private_key"] = Path.Combine(sample, "es256_private.key"),
            ["sign_cert"] = Path.Combine(sample, "es256_certs.pem"),
        };
        definition.AddRange(manifest);

        var dir = Path.GetDirectoryName(output)!;
        var manifestPath = Path.Combine(Path.GetTempPath(), "eg-c2pa-manifest.json");
        var settingsPath = Path.Combine(Path.GetTempPath(), "eg-c2pa-settings.json");
        File.WriteAllText(manifestPath, PyJson.Dumps(definition, ascii: false));
        File.WriteAllText(settingsPath, """{"version": 1, "builder": {"thumbnail": {"enabled": false}}, "verify": {"verify_trust": false, "remote_manifest_fetch": false}}""");

        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
        foreach (var arg in (string[])[input, "--settings", settingsPath, "-m", manifestPath, "-o", output, "-f"])
        {
            psi.ArgumentList.Add(arg);
        }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(output))
        {
            throw new InvalidOperationException($"c2patool failed on {input}: {stderr}{stdout}");
        }
    }

    #endregion // Public methods
}


/// <summary>
/// Plain-text payloads (A1111 infotext and friends); line breaks are always LF.
/// </summary>
static class Texts
{
    #region Properties

    public static string A1111 => Lf("""
        masterpiece, 1girl, (detailed eyes:1.2), <lora:add_detail:0.8>, embedding:easynegative
        second prompt line, BREAK sunset
        Negative prompt: lowres, bad anatomy, easynegative
        Steps: 28, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 7, Seed: 1234567890, Size: 832x1216, Model hash: 6ce0161689, Model: realisticVision_v51, VAE hash: 735e4c3a44, VAE: vae-ft-mse-840000, Denoising strength: 0.4, Clip skip: 2, ADetailer model: face_yolov8n.pt, ADetailer prompt: "smiling, blue eyes", ADetailer confidence: 0.3, ADetailer denoising strength: 0.4, ADetailer model 2nd: hand_yolov8n.pt, ADetailer confidence 2nd: 0.3, ADetailer denoising strength 2nd: 0.4, ADetailer version: 24.11.1, ControlNet 0: "Module: canny, Model: control_v11p_sd15_canny [d14c016b], Weight: 1.0, Resize Mode: Crop and Resize, Processor Res: 512, Threshold A: 100.0, Threshold B: 200.0, Guidance Start: 0.0, Guidance End: 1.0, Pixel Perfect: False, Control Mode: Balanced", Hires prompt: "a \"quoted\" hires prompt,\nwith newline", Hires upscale: 2, Hires steps: 15, Hires upscaler: 4x-UltraSharp, Lora hashes: "add_detail: 7c6bad76eb54", TI hashes: "easynegative: c74b4e810b03", Emphasis: Original, Version: v1.10.1
        """);

    public static string A1111Utf8 => Lf("""
        猫の少女, 🌸 cherry blossom
        Negative prompt: worst quality
        Steps: 20, Sampler: Euler a, CFG scale: 7, Seed: 1, Size: 512x512, Model hash: abc, Version: v1.10.1
        """);

    public static string Mojibake => Lf("""
        猫の少女 café
        Steps: 20, Sampler: Euler a, CFG scale: 7
        """);

    public static string Forge => Lf("""
        a futuristic city at night, neon lights
        Steps: 20, Sampler: Euler, Schedule type: Simple, CFG scale: 1, Distilled CFG Scale: 3.5, Seed: 42, Size: 896x1152, Model hash: 275ef623d3, Model: flux1-dev-bnb-nf4-v2, Lora hashes: "flux_realism: a1b2c3d4e5f6", Module 1: ae, Module 2: clip_l, Module 3: t5xxl_fp16, Diffusion in Low Bits: Automatic (fp16 LoRA), Version: f2.0.1v1.10.1-previous-669-gdfdcbab6
        """);

    public static string SdNext => Lf("""
        a cat wearing a wizard hat
        Negative prompt: lowres, blurry
        Steps: 20, Seed: 1620085323, Sampler: DPM++ 2M, CFG scale: 6, Size: 1024x1024, Batch: 1x1, Model: sdxl-base-1.0, Model hash: 31e35c80fc, VAE: sdxl-vae, App: SD.Next, Version: 0a0cbd5e, Pipeline: StableDiffusionXLPipeline, Operations: txt2img; hires, Parser: native, Hires upscaler: ESRGAN 4x
        Template: masterpiece, {prompt}
        """);

    public static string CivitaiGenerator => Lf("""
        a portrait of a knight in silver armor
        Negative prompt: bad hands, lowres
        Steps: 25, Sampler: DPM++ 2M Karras, CFG scale: 7, Seed: 1234567, Size: 832x1216, Clip skip: 2, Created Date: 2026-08-26T18:33:26.71Z, Civitai resources: [{"type":"checkpoint","modelVersionId":290640,"modelName":"Pony Diffusion V6 XL","modelVersionName":"V6 (start with this one)"},{"type":"lora","weight":0.8,"modelVersionId":12345,"modelName":"Detail Tweaker \u0027XL\u0027","modelVersionName":"v1.0"}], Civitai metadata: {"workflow":"txt2img","remixOfId":1234567,"resources":[{"modelVersionId":290640,"strength":1,"type":"Checkpoint"}]}
        """);

    public static string CivitaiExtension => Lf("""
        a portrait of a knight, <lora:add_detail:0.6>
        Negative prompt: lowres
        Steps: 30, Sampler: Euler a, CFG scale: 7, Seed: 99, Size: 512x768, Model hash: 6ce0161689, Model: realisticVision_v51, Lora hashes: "add_detail: 7c6bad76eb54", Hashes: {"vae": "735e4c3a44", "lora:add_detail": "7c6bad76eb", "model": "6ce0161689"}, Version: v1.10.1
        """);

    public static string Krita => Lf("""
        a watercolor fox <lora:ink:0.5>
        Negative prompt: photo
        Steps: 20, Sampler: Euler a, CFG scale: 7.0, Seed: 5, Size: 768x768, Model hash: unknown, Model: zavychroma
        """);

    public static string FooocusA1111 => Lf("""
        a cat café, cinematic, high detail
        Negative prompt: unrealistic, saturated
        Steps: 30, Sampler: DPM++ 2M SDE Karras, Seed: 123, Size: 1152x896, CFG scale: 4, Sharpness: 2, ADM Guidance: "(1.5, 0.8, 0.3)", Model: juggernautXL_v8Rundiffusion, Model hash: aeb7e9e689, Performance: Speed, Scheduler: karras, VAE: Default (model), Refiner: None, Lora hashes: "sd_xl_offset_example-lora_1.0: 4852686128", Lora weights: "sd_xl_offset_example-lora_1.0: 0.1", Raw prompt: a cat café, Raw negative prompt: "", Styles: "['Fooocus V2', 'Fooocus Enhance']", Version: Fooocus v2.5.5
        """);

    public static string InvokeDream => "\"a castle on a hill\" -s 50 -S 3357757885 -W 512 -H 512 -C 7.5 -A k_lms";

    public static string Midjourney => "a koi pond in autumn --ar 16:9 --stylize 250 --v 7 Job ID: 0123abcd-4567-89ef-0123-456789abcdef";

    #endregion // Properties


    #region Public methods

    public static string Lf(string s) => s.ReplaceLineEndings("\n");

    #endregion // Public methods
}


/// <summary>
/// JSON payloads, built in the key order each generator writes.
/// </summary>
static class Payloads
{
    #region Public methods

    public static J ComfyPrompt() => new()
    {
        ["3"] = Node("KSampler", "KSampler", new J
        {
            ["seed"] = 156680208700286L, ["steps"] = 20, ["cfg"] = 8.0, ["sampler_name"] = "euler", ["scheduler"] = "normal", ["denoise"] = 1.0,
            ["model"] = Link("10", 0), ["positive"] = Link("6", 0), ["negative"] = Link("7", 0), ["latent_image"] = Link("5", 0),
        }),
        ["4"] = Node("CheckpointLoaderSimple", "Load Checkpoint", new J { ["ckpt_name"] = "v1-5-pruned-emaonly.safetensors" }),
        ["5"] = Node("EmptyLatentImage", "Empty Latent Image", new J { ["width"] = 512, ["height"] = 512, ["batch_size"] = 1 }),
        ["6"] = Node("CLIPTextEncode", "CLIP Text Encode (Prompt)", new J { ["text"] = "beautiful scenery, 日本の風景", ["clip"] = Link("10", 1) }),
        ["7"] = Node("CLIPTextEncode", "CLIP Text Encode (Prompt)", new J { ["text"] = "text, watermark", ["clip"] = Link("10", 1) }),
        ["8"] = Node("VAEDecode", "VAE Decode", new J { ["samples"] = Link("3", 0), ["vae"] = Link("4", 2) }),
        ["9"] = Node("SaveImage", "Save Image", new J { ["filename_prefix"] = "ComfyUI", ["images"] = Link("8", 0) }),
        ["10"] = Node("LoraLoader", "Load LoRA", new J
        {
            ["lora_name"] = "add_detail.safetensors", ["strength_model"] = 0.8, ["strength_clip"] = 0.8, ["model"] = Link("4", 0), ["clip"] = Link("4", 1),
        }),
        ["57:12"] = Node("PrimitiveFloat", "Float (in subgraph)", new J { ["value"] = double.NaN }),
    };


    public static J ComfyWorkflow() => new()
    {
        ["id"] = "00000000-0000-0000-0000-000000000001", ["revision"] = 0, ["last_node_id"] = 10, ["last_link_id"] = 9,
        ["nodes"] = new object?[]
        {
            new J
            {
                ["id"] = 3, ["type"] = "KSampler", ["pos"] = new object?[] { 863, 186 }, ["size"] = new object?[] { 315, 262 }, ["flags"] = new J(),
                ["order"] = 4, ["mode"] = 0,
                ["inputs"] = new object?[] { new J { ["name"] = "model", ["type"] = "MODEL", ["link"] = 1 } },
                ["outputs"] = new object?[] { new J { ["name"] = "LATENT", ["type"] = "LATENT", ["links"] = new object?[] { 7 } } },
                ["properties"] = new J { ["Node name for S&R"] = "KSampler" },
                ["widgets_values"] = new object?[] { 156680208700286L, "randomize", 20, 8, "euler", "normal", 1 },
            },
        },
        ["links"] = new object?[] { new object?[] { 1, 4, 0, 3, 0, "MODEL" } }, ["groups"] = new object?[0], ["config"] = new J(),
        ["extra"] = new J { ["ds"] = new J { ["scale"] = 1, ["offset"] = new object?[] { 0, 0 } } }, ["version"] = 0.4,
    };


    /// <summary>
    /// Flux on SamplerCustomAdvanced; the prompt text comes through a string node and the upscale runs in subgraph 57.
    /// </summary>
    public static J ComfyFluxPrompt() => new()
    {
        ["10"] = Node("UNETLoader", "Load Diffusion Model", new J { ["unet_name"] = "flux1-dev.safetensors", ["weight_dtype"] = "default" }),
        ["11"] = Node("DualCLIPLoader", "DualCLIPLoader", new J { ["clip_name1"] = "t5xxl_fp16.safetensors", ["clip_name2"] = "clip_l.safetensors", ["type"] = "flux" }),
        ["12"] = Node("VAELoader", "Load VAE", new J { ["vae_name"] = "ae.safetensors" }),
        ["13"] = Node("LoraLoaderModelOnly", "LoraLoaderModelOnly", new J { ["lora_name"] = "flux_realism.safetensors", ["strength_model"] = 0.7, ["model"] = Link("10", 0) }),
        ["20"] = Node("PrimitiveStringMultiline", "Prompt", new J { ["value"] = "a red fox in the snow, 雪, golden hour" }),
        ["21"] = Node("CLIPTextEncode", "CLIP Text Encode (Positive Prompt)", new J { ["text"] = Link("20", 0), ["clip"] = Link("11", 0) }),
        ["22"] = Node("FluxGuidance", "FluxGuidance", new J { ["guidance"] = 3.5, ["conditioning"] = Link("21", 0) }),
        ["25"] = Node("RandomNoise", "RandomNoise", new J { ["noise_seed"] = 987654321L }),
        ["26"] = Node("KSamplerSelect", "KSamplerSelect", new J { ["sampler_name"] = "euler" }),
        ["27"] = Node("BasicScheduler", "BasicScheduler", new J { ["scheduler"] = "simple", ["steps"] = 20, ["denoise"] = 1.0, ["model"] = Link("13", 0) }),
        ["28"] = Node("BasicGuider", "BasicGuider", new J { ["model"] = Link("13", 0), ["conditioning"] = Link("22", 0) }),
        ["29"] = Node("EmptySD3LatentImage", "EmptySD3LatentImage", new J { ["width"] = 1024, ["height"] = 1024, ["batch_size"] = 1 }),
        ["30"] = Node("SamplerCustomAdvanced", "SamplerCustomAdvanced", new J
        {
            ["noise"] = Link("25", 0), ["guider"] = Link("28", 0), ["sampler"] = Link("26", 0), ["sigmas"] = Link("27", 0), ["latent_image"] = Link("29", 0),
        }),
        ["31"] = Node("VAEDecode", "VAE Decode", new J { ["samples"] = Link("30", 0), ["vae"] = Link("12", 0) }),
        ["57:40"] = Node("UpscaleModelLoader", "Load Upscale Model", new J { ["model_name"] = "4x-UltraSharp.pth" }),
        ["57:41"] = Node("ImageUpscaleWithModel", "Upscale Image (using Model)", new J { ["upscale_model"] = Link("57:40", 0), ["image"] = Link("31", 0) }),
        ["57:42"] = Node("PrimitiveFloat", "Max size", new J { ["value"] = double.PositiveInfinity }),
        ["9"] = Node("SaveImage", "Save Image", new J { ["filename_prefix"] = "flux", ["images"] = Link("57:41", 0) }),
    };


    /// <summary>
    /// Workflow schema 1.0: links as objects, the subgraph definition under definitions.subgraphs.
    /// </summary>
    public static J ComfyFluxWorkflow()
    {
        const string SUBGRAPH = "8b1f2c3d-0000-4000-8000-000000000057";
        return new()
        {
            ["id"] = "00000000-0000-0000-0000-000000000002", ["revision"] = 0, ["last_node_id"] = 57, ["last_link_id"] = 3,
            ["nodes"] = new object?[]
            {
                UiNode(20, "PrimitiveStringMultiline", new object?[] { "a red fox in the snow, 雪, golden hour" }),
                UiNode(30, "SamplerCustomAdvanced", new object?[0]),
                UiNode(57, SUBGRAPH, new object?[0]),
                UiNode(9, "SaveImage", new object?[] { "flux" }),
            },
            ["links"] = new object?[]
            {
                new J { ["id"] = 1, ["origin_id"] = 30, ["origin_slot"] = 0, ["target_id"] = 57, ["target_slot"] = 0, ["type"] = "IMAGE" },
                new J { ["id"] = 2, ["origin_id"] = 57, ["origin_slot"] = 0, ["target_id"] = 9, ["target_slot"] = 0, ["type"] = "IMAGE" },
            },
            ["groups"] = new object?[0],
            ["definitions"] = new J
            {
                ["subgraphs"] = new object?[]
                {
                    new J
                    {
                        ["id"] = SUBGRAPH, ["version"] = 1, ["name"] = "Upscale", ["revision"] = 0,
                        ["inputs"] = new object?[] { new J { ["name"] = "image", ["type"] = "IMAGE" } },
                        ["outputs"] = new object?[] { new J { ["name"] = "IMAGE", ["type"] = "IMAGE" } },
                        ["nodes"] = new object?[]
                        {
                            UiNode(40, "UpscaleModelLoader", new object?[] { "4x-UltraSharp.pth" }),
                            UiNode(41, "ImageUpscaleWithModel", new object?[0]),
                        },
                        ["links"] = new object?[]
                        {
                            new J { ["id"] = 3, ["origin_id"] = 40, ["origin_slot"] = 0, ["target_id"] = 41, ["target_slot"] = 0, ["type"] = "UPSCALE_MODEL" },
                        },
                    },
                },
            },
            ["config"] = new J(), ["extra"] = new J { ["frontendVersion"] = "1.28.0" }, ["version"] = 1,
        };
    }


    /// <summary>
    /// A schema 0.4 workflow with enough CLIPTextEncode nodes to pass the given JSON length.
    /// </summary>
    public static J BigWorkflow(int minLength)
    {
        var nodes = new List<object?>();
        var links = new List<object?>();
        var text = string.Concat(Enumerable.Repeat("highly detailed, intricate, ", 12));
        for (var id = 1; ; id++)
        {
            nodes.Add(new J
            {
                ["id"] = id, ["type"] = "CLIPTextEncode", ["pos"] = new object?[] { id % 40 * 420, id / 40 * 260 }, ["size"] = new object?[] { 400, 200 },
                ["flags"] = new J(), ["order"] = id, ["mode"] = 0,
                ["inputs"] = new object?[] { new J { ["name"] = "clip", ["type"] = "CLIP", ["link"] = id } },
                ["outputs"] = new object?[] { new J { ["name"] = "CONDITIONING", ["type"] = "CONDITIONING", ["links"] = new object?[] { id + 1 } } },
                ["properties"] = new J { ["Node name for S&R"] = "CLIPTextEncode" },
                ["widgets_values"] = new object?[] { $"node {id}: {text}" },
            });
            links.Add(new object?[] { id, id, 0, id + 1, 0, "CONDITIONING" });
            if (id % 100 == 0 && PyJson.Dumps(Wrap()).Length >= minLength) return Wrap();
        }

        J Wrap() => new()
        {
            ["id"] = "00000000-0000-0000-0000-000000000003", ["revision"] = 0, ["last_node_id"] = nodes.Count, ["last_link_id"] = links.Count,
            ["nodes"] = nodes, ["links"] = links, ["groups"] = new object?[0], ["config"] = new J(), ["extra"] = new J(), ["version"] = 0.4,
        };
    }


    public static J InvokeMetadata() => new()
    {
        ["generation_mode"] = "sdxl_txt2img", ["positive_prompt"] = "a lighthouse at dusk", ["negative_prompt"] = "blurry",
        ["width"] = 1024, ["height"] = 1024, ["seed"] = 42, ["rand_device"] = "cpu", ["cfg_scale"] = 6.5, ["cfg_rescale_multiplier"] = 0,
        ["steps"] = 30, ["scheduler"] = "dpmpp_2m_k",
        ["model"] = new J { ["key"] = "3f1c...", ["hash"] = "blake3:abcd", ["name"] = "Juggernaut XL v9", ["base"] = "sdxl", ["type"] = "main" },
        ["loras"] = new object?[]
        {
            new J
            {
                ["model"] = new J { ["key"] = "k1", ["hash"] = "blake3:ef01", ["name"] = "detail-tweaker-xl", ["base"] = "sdxl", ["type"] = "lora" },
                ["weight"] = 0.75,
            },
        },
        ["app_version"] = "6.9.0", ["metadata_version"] = "1.0.0",
    };


    public static J InvokeWorkflow() => new() { ["name"] = "", ["nodes"] = new object?[0], ["edges"] = new object?[0], ["meta"] = new J { ["version"] = "3.0.0" } };

    public static J InvokeGraph() => new() { ["id"] = "g1", ["nodes"] = new J(), ["edges"] = new object?[0] };


    public static J InvokeLegacy() => new()
    {
        ["model"] = "stable diffusion", ["model_weights"] = "stable-diffusion-1.5", ["app_id"] = "invoke-ai/InvokeAI", ["app_version"] = "2.3.5",
        ["image"] = new J
        {
            ["prompt"] = new object?[] { new J { ["prompt"] = "a castle on a hill", ["weight"] = 1.0 } },
            ["steps"] = 50, ["cfg_scale"] = 7.5, ["seed"] = 3357757885L, ["width"] = 512, ["height"] = 512, ["sampler"] = "k_lms", ["type"] = "txt2img",
        },
    };


    public static J NovelAiChunks() => new()
    {
        ["Title"] = "NovelAI generated image",
        ["Description"] = "1girl, solo, rain",
        ["Software"] = "NovelAI",
        ["Source"] = "NovelAI Diffusion V4.5 4BDE2A90",
        ["Generation time"] = "3.2",
        ["Comment"] = PyJson.Dumps(new J
        {
            ["prompt"] = "1girl, solo, rain", ["steps"] = 28, ["height"] = 1216, ["width"] = 832, ["scale"] = 5.0, ["uncond_scale"] = 0.0,
            ["cfg_rescale"] = 0.0, ["seed"] = 2222, ["n_samples"] = 1, ["noise_schedule"] = "karras", ["sampler"] = "k_euler_ancestral",
            ["v4_prompt"] = new J
            {
                ["caption"] = new J
                {
                    ["base_caption"] = "1girl, solo, rain",
                    ["char_captions"] = new object?[] { new J { ["char_caption"] = "girl, umbrella", ["centers"] = new object?[] { new J { ["x"] = 0.5, ["y"] = 0.5 } } } },
                },
                ["use_coords"] = false, ["use_order"] = true,
            },
            ["v4_negative_prompt"] = new J { ["caption"] = new J { ["base_caption"] = "lowres", ["char_captions"] = new object?[0] }, ["legacy_uc"] = false },
            ["uc"] = "lowres", ["request_type"] = "PromptGenerateRequest", ["signed_hash"] = "AAAA...==",
        }),
    };


    public static J FooocusMeta() => new()
    {
        ["adm_guidance"] = "(1.5, 0.8, 0.3)", ["base_model"] = "juggernautXL_v8Rundiffusion", ["base_model_hash"] = "aeb7e9e689",
        ["full_negative_prompt"] = new object?[] { "unrealistic", "saturated" }, ["full_prompt"] = new object?[] { "a cat, cinematic", "high detail" },
        ["guidance_scale"] = 4, ["loras"] = new object?[] { new object?[] { "sd_xl_offset_example-lora_1.0", 0.1, "4852686128" } },
        ["negative_prompt"] = "", ["performance"] = "Speed", ["prompt"] = "a cat café ☕", ["refiner_model"] = "None",
        ["refiner_switch"] = 0.5, ["resolution"] = "(1152, 896)", ["sampler"] = "dpmpp_2m_sde_gpu", ["scheduler"] = "karras",
        ["seed"] = "123", ["sharpness"] = 2, ["steps"] = 30, ["styles"] = "['Fooocus V2', 'Fooocus Enhance']",
        ["vae"] = "Default (model)", ["version"] = "Fooocus v2.5.5",
    };


    public static J FooocusMre() => new()
    {
        ["prompt"] = "a cat in a garden, ☀", ["negative_prompt"] = "blurry", ["styles"] = "['Fooocus V2']",
        ["real_prompt"] = "a cat in a garden, ☀, highly detailed", ["real_negative_prompt"] = "blurry",
        ["seed"] = 1234, ["width"] = 1152, ["height"] = 896, ["sampler_name"] = "dpmpp_2m_sde_gpu", ["scheduler"] = "karras",
        ["base_model_name"] = "sd_xl_base_1.0_0.9vae.safetensors", ["refiner_model_name"] = "sd_xl_refiner_1.0_0.9vae.safetensors",
        ["steps"] = 30, ["switch"] = 20, ["sharpness"] = 2.0, ["cfg"] = 7.0, ["l1"] = "sd_xl_offset_example-lora_1.0.safetensors", ["w1"] = 0.5,
        ["software"] = "Fooocus 2.0.78.5 MRE",
    };


    public static J RuinedFooocus() => new()
    {
        ["Prompt"] = "a lighthouse on a cliff", ["Negative"] = "lowres", ["steps"] = 30, ["cfg"] = 7.0, ["width"] = 1152, ["height"] = 896,
        ["seed"] = 4242, ["sampler_name"] = "dpmpp_2m_sde_gpu", ["scheduler"] = "karras",
        ["base_model_name"] = "sd_xl_base_1.0.safetensors", ["base_model_hash"] = "31e35c80fc",
        ["loras"] = new object?[] { new object?[] { "0123456789abcdef", "0.5 - detail_tweaker_xl.safetensors" } },
        ["start_step"] = 0, ["denoise"] = 1.0, ["clip_skip"] = 1, ["comment"] = "", ["software"] = "RuinedFooocus",
    };


    /// <summary>
    /// SwarmUI: LoRAs as folder paths without extension, weights as strings.
    /// </summary>
    public static J Swarm() => new()
    {
        ["sui_image_params"] = new J
        {
            ["prompt"] = "a red fox, 狐", ["negativeprompt"] = "", ["model"] = "OfficialStableDiffusion/sd_xl_base_1.0", ["seed"] = 1557018183,
            ["steps"] = 20, ["cfgscale"] = 7, ["aspectratio"] = "1:1", ["width"] = 1024, ["height"] = 1024, ["sampler"] = "euler",
            ["scheduler"] = "normal", ["loras"] = new object?[] { "Styles/detail-tweaker-xl" }, ["loraweights"] = new object?[] { "0.8" },
            ["swarm_version"] = "0.9.6.2",
        },
        ["sui_extra_data"] = new J { ["date"] = "2025-10-01", ["prep_time"] = "0.01 sec", ["generation_time"] = "3.20 sec" },
        ["sui_models"] = new object?[]
        {
            new J { ["name"] = "OfficialStableDiffusion/sd_xl_base_1.0.safetensors", ["param"] = "model", ["hash"] = "0x" + string.Concat(Enumerable.Repeat("ab", 32)) },
            new J { ["name"] = "Styles/detail-tweaker-xl.safetensors", ["param"] = "loras", ["hash"] = "0x" + string.Concat(Enumerable.Repeat("cd", 32)) },
        },
    };


    public static J EasyDiffusion() => new()
    {
        ["prompt"] = "a photograph of an astronaut riding a horse", ["negative_prompt"] = "", ["seed"] = "1234",
        ["use_stable_diffusion_model"] = "sd-v1-5", ["sampler_name"] = "euler_a", ["num_inference_steps"] = "25",
        ["guidance_scale"] = "7.5", ["width"] = "512", ["height"] = "512", ["use_vae_model"] = "", ["clip_skip"] = "False",
    };


    public static J DrawThings() => new()
    {
        ["c"] = "a cozy cabin, <snow>", ["uc"] = "blurry", ["model"] = "flux_1_schnell_q8p.ckpt",
        ["lora"] = new object?[] { new J { ["file"] = "detail_f16.ckpt", ["weight"] = 0.6 } },
        ["sampler"] = "Euler A Trailing", ["scale"] = 1.0, ["seed"] = 99, ["seed_mode"] = "Scale Alike", ["size"] = "1024x1024", ["steps"] = 4,
        ["strength"] = 1.0, ["v2"] = new J { ["guidanceEmbed"] = 3.5 },
    };


    /// <summary>
    /// Tensor.art generation_data (community-documented keys).
    /// </summary>
    public static J TensorArt() => new()
    {
        ["prompt"] = "a snowy mountain village at dusk", ["negativePrompt"] = "lowres", ["steps"] = 25, ["ksamplerName"] = "dpmpp_2m",
        ["schedule"] = "karras", ["cfgScale"] = 7, ["seed"] = "123456789", ["clipSkip"] = 2, ["width"] = 768, ["height"] = 1152,
        ["baseModel"] = new J { ["label"] = "Example XL", ["modelId"] = "600000000000000001" },
        ["models"] = new object?[]
        {
            new J { ["label"] = "Example LoRA", ["type"] = "LORA", ["weight"] = 0.7, ["modelFileName"] = "example_lora.safetensors", ["baseModel"] = "SDXL", ["hash"] = "ABCDEF0123" },
        },
    };


    public static J TensorArtPrompt() => new()
    {
        ["1"] = Node("ECHOCheckpointLoaderSimple", "Load Checkpoint", new J { ["ckpt_name"] = "EMS-600000000000000001-EMS.safetensors" }),
        ["2"] = Node("CLIPTextEncode", "Positive", new J { ["text"] = "a snowy mountain village at dusk", ["clip"] = Link("1", 1) }),
        ["3"] = Node("CLIPTextEncode", "Negative", new J { ["text"] = "lowres", ["clip"] = Link("1", 1) }),
        ["4"] = Node("EmptyLatentImage", "Empty Latent Image", new J { ["width"] = 768, ["height"] = 1152, ["batch_size"] = 1 }),
        ["5"] = Node("KSampler", "KSampler", new J
        {
            ["seed"] = 123456789L, ["steps"] = 25, ["cfg"] = 7, ["sampler_name"] = "dpmpp_2m", ["scheduler"] = "karras", ["denoise"] = 1,
            ["model"] = Link("1", 0), ["positive"] = Link("2", 0), ["negative"] = Link("3", 0), ["latent_image"] = Link("4", 0),
        }),
    };


    /// <summary>
    /// GB 45438-2025 implicit label: producer and propagator ids are made up.
    /// </summary>
    public static J Aigc() => new()
    {
        ["Label"] = "1", ["ContentProducer"] = "001191110108MA01KP2T7U00000", ["ProduceID"] = "kling-0123456789abcdef",
        ["ReservedCode1"] = "", ["ContentPropagator"] = "001191110108MA01KP2T7U00000", ["PropagateID"] = "kling-0123456789abcdef",
        ["ReservedCode2"] = "",
    };


    /// <summary>
    /// A character card as base64 JSON (Tavern/SillyTavern style).
    /// </summary>
    public static string CharacterCard(string spec, string version) => Convert.ToBase64String(Bytes.Utf8(PyJson.Dumps(new J
    {
        ["spec"] = spec, ["spec_version"] = version,
        ["data"] = new J { ["name"] = "Test Character", ["description"] = "A helpful test character.", ["first_mes"] = "Hello!" },
    }, ascii: false)));

    #endregion // Public methods


    #region Protected and private methods

    private static J Node(string type, string title, J inputs) => new() { ["inputs"] = inputs, ["class_type"] = type, ["_meta"] = new J { ["title"] = title } };

    private static object?[] Link(string id, int slot) => [id, slot];


    private static J UiNode(int id, string type, object?[] widgets) => new()
    {
        ["id"] = id, ["type"] = type, ["pos"] = new object?[] { id * 10, 0 }, ["size"] = new object?[] { 300, 100 }, ["flags"] = new J(),
        ["order"] = id, ["mode"] = 0, ["inputs"] = new object?[0], ["outputs"] = new object?[0], ["properties"] = new J(), ["widgets_values"] = widgets,
    };

    #endregion // Protected and private methods
}


/// <summary>
/// c2patool manifest definitions shaped like each vendor's published manifests.
/// </summary>
static class Manifests
{
    #region Fields and constants

    private const string DST = "http://cv.iptc.org/newscodes/digitalsourcetype/";

    #endregion // Fields and constants


    #region Public methods

    public static J ChatGptIngredient() => V2("ChatGPT", null, "image.png",
        Action("c2pa.created", new J { ["name"] = "GPT-4o" }, DST + "trainedAlgorithmicMedia"), Action("c2pa.converted", null, null));

    public static J ChatGptActive() => V2("ChatGPT", null, "image.png");

    public static J OpenAiApi() => V2("OpenAI Media Service API", null, "image.png",
        Action("c2pa.created", new J { ["name"] = "gpt-image", ["version"] = "2.0" }, DST + "trainedAlgorithmicMedia"),
        Action("c2pa.converted", null, null), Action("c2pa.watermarked.unbound", null, null));

    public static J DalleIngredient() => V1("OpenAI-API", "image.webp",
        Action("c2pa.created", "DALL\u00B7E", DST + "trainedAlgorithmicMedia"), Action("c2pa.converted", null, null));

    public static J DalleActive() => V1("ChatGPT", "image.webp");

    public static J Firefly()
    {
        var created = Action("c2pa.created", "Adobe Firefly", DST + "trainedAlgorithmicMedia");
        created["parameters"] = new J { ["com.adobe.firefly.operation"] = "text_to_image", ["com.adobe.firefly.version"] = "4.0.0-release-firefly_v4" };
        return V1("Adobe_Firefly", "Generated image", created);
    }

    public static J Capture() => V2("Example Camera", "1.0", "IMG_0001.jpg", Action("c2pa.created", null, DST + "digitalCapture"));

    public static J GenerativeFill() => V2("Adobe Photoshop", "26.11.0", "IMG_0001.jpg",
        Action("c2pa.edited", new J { ["name"] = "Adobe Firefly" }, DST + "compositeWithTrainedAlgorithmicMedia", "Generative Fill"));

    public static J GoogleGenerated() => V2("Google C2PA Core Generator Library", "1.0:1.0", "image.png",
        Action("c2pa.created", null, DST + "trainedAlgorithmicMedia", "Created by Google Generative AI."),
        Action("c2pa.edited", null, DST + "trainedAlgorithmicMedia", "Applied imperceptible SynthID watermark."));

    public static J GoogleWrapper() => V2("Google C2PA Core Generator Library", "1.0:1.0", "image.png",
        Action("c2pa.edited", null, DST + "trainedAlgorithmicMedia", "Added imperceptible SynthID watermark"),
        Action("c2pa.converted", null, null, "Converted to .png"));

    public static J Microsoft() => V1("Microsoft Responsible AI Image Provenance", "image.png",
        Action("c2pa.created", "Azure OpenAI DALL-E", null, "AI Generated Image"));

    public static J Sora() => V2("Sora", null, "video.mp4",
        Action("c2pa.created", new J { ["name"] = "Sora" }, DST + "trainedAlgorithmicMedia"));

    #endregion // Public methods


    #region Protected and private methods

    private static J V2(string generator, string? version, string title, params J[] actions)
    {
        var info = new J { ["name"] = generator };
        if (version is not null) info["version"] = version;
        var m = new J { ["claim_generator_info"] = new object?[] { info }, ["title"] = title };
        if (actions.Length > 0) m["assertions"] = new object?[] { ActionsAssertion("c2pa.actions.v2", actions) };
        return m;
    }


    // c2pa-rs builds the v1 claim_generator string from claim_generator_info and writes actions as c2pa.actions.v2
    private static J V1(string generator, string title, params J[] actions)
    {
        var m = new J { ["claim_version"] = 1, ["claim_generator_info"] = new object?[] { new J { ["name"] = generator } }, ["title"] = title };
        if (actions.Length > 0) m["assertions"] = new object?[] { ActionsAssertion("c2pa.actions", actions) };
        return m;
    }


    private static J ActionsAssertion(string label, J[] actions) =>
        new() { ["label"] = label, ["data"] = new J { ["actions"] = actions.Cast<object?>().ToArray() } };


    private static J Action(string action, object? agent, string? sourceType, string? description = null)
    {
        var a = new J { ["action"] = action };
        if (agent is not null) a["softwareAgent"] = agent;
        if (sourceType is not null) a["digitalSourceType"] = sourceType;
        if (description is not null) a["description"] = description;
        return a;
    }

    #endregion // Protected and private methods
}
