# AI metadata fixtures

Synthetic files that copy how each AI generator writes its metadata: tiny images (16 x 16 solid colour), no model output and no third-party content. The AI corpus tests read them with the bundled ExifTool started with `source/__assets/exiftool/exifglass.config`.

Made by `source/tools/gen-ai-fixtures.cs`, which also writes this README. From `source/tools`:

```
dotnet run gen-ai-fixtures.cs                           # rewrite every fixture except the signed C2PA files
dotnet run gen-ai-fixtures.cs -- --check                # regenerate into a temp folder and compare byte for byte
dotnet run gen-ai-fixtures.cs -- --c2patool <c2patool>  # also re-sign the C2PA files (c2patool's sample/ folder next to it)
```

Everything except the signed C2PA files is reproducible byte for byte on any OS: fixed ids and seeds, no clock, stored (uncompressed) deflate, and nothing time-based from ExifTool. The signed C2PA files carry random manifest ids and ECDSA signatures, so they are committed and change only when re-signed. They use c2patool's public test certificate (`sample/es256_certs.pem`, "C2PA Test Signing Cert", valid to 2030) and fail trust checks on purpose. The video, audio and EXR files hold metadata only, no frames. Bytes marked `~` change when re-signed.

## A1111 family (A1111, Forge, SD.Next, Civitai, Krita AI Diffusion)

| File | Bytes | Mimics |
|---|---:|---|
| `a1111.png` | 2,061 | A1111 `images.py`: Pillow `PngInfo.add_text("parameters")`, Latin-1 so tEXt; ADetailer 2nd unit, ControlNet, quoted hires prompt |
| `a1111_utf8.png` | 1,044 | A1111 with non-Latin-1 text: Pillow falls back to iTXt |
| `a1111_mojibake.png` | 935 | a non-compliant writer that puts UTF-8 bytes in tEXt (ExifTool reads tEXt as cp1252: mojibake) |
| `a1111_ztxt.png` | 253 | zTXt (compressed Latin-1) `parameters` and compressed iTXt `workflow` |
| `a1111_text_after_idat.png` | 2,089 | text chunks after IDAT (skipped by `-fast2`) |
| `a1111.jpg` | 3,032 | A1111 JPEG: piexif `UserComment` UNICODE + UTF-16BE in a big-endian TIFF; `piexif.insert` replaces APP0 |
| `a1111.webp` | 2,500 | A1111 WebP: the piexif bytes through Pillow `exif=`, which drops the `Exif\0\0` header of a still WebP |
| `a1111.gif` | 1,256 | A1111 GIF: Pillow `comment=` (UTF-8 comment extension) |
| `a1111_sidecar.png` | 852 | A1111 `save_txt`: no metadata in the image, the infotext in `a1111_sidecar.txt` |
| `a1111_sidecar.txt` | 1,186 | the sidecar text (LF line endings) |
| `forge.png` | 1,272 | Forge (Flux): `Distilled CFG Scale`, `Module N`, `Diffusion in Low Bits`, `Version: f2.0.1v1.10.1-previous-669-g...` |
| `sdnext.png` | 1,257 | SD.Next `save.py`: `App: SD.Next`, `Version` (8-hex git hash), `Pipeline`, `Operations`, a `Template:` line |
| `sdnext.jxl` | 928 | SD.Next JXL through pillow-jxl 1.3.7: raw TIFF in the `Exif` box, without the 4-byte offset the spec asks for |
| `sdnext_spec.jxl` | 932 | the same EXIF in a spec-compliant JXL `Exif` box (4-byte TIFF header offset first) |
| `civitai.jpg` | 1,984 | Civitai on-site JPEG: UNICODE + UTF-16BE inside a little-endian TIFF, IFD0 `Artist: ai`, `Software: <GUID>` |
| `civitai.png` | 2,204 | Civitai on-site PNG: the same EXIF in an `eXIf` chunk, no text chunk |
| `civitai_extension.png` | 1,209 | A1111 + Civitai extension `gen_hashing.py`: unquoted `Hashes: {...}` at the end |
| `krita.png` | 1,041 | Krita AI Diffusion `image.py`: uncompressed iTXt `parameters` right after IHDR, `Model hash: unknown` |
| `krita_cloud.png` | 1,515 | Krita AI Diffusion cloud mode: plus XMP `Iptc4xmpExt:DigitalSourceType` (trainedAlgorithmicMedia) |

## ComfyUI

| File | Bytes | Mimics |
|---|---:|---|
| `comfyui.png` | 2,947 | `SaveImage` (`nodes.py`): tEXt `prompt` + `workflow`, `json.dumps` defaults (ASCII `\uXXXX`, bare `NaN`) |
| `comfyui_flux_subgraph.png` | 5,436 | Flux on SamplerCustomAdvanced, prompt through a string node, `Infinity`; workflow schema 1.0 with a subgraph |
| `comfyui_anim.webp` | 2,376 | `SaveAnimatedWEBP`: Pillow animated WebP, EXIF Make `workflow:` / Model `prompt:` (`Exif\0\0` kept) |
| `comfyui_anim.png` | 4,703 | `SaveAnimatedPNG`: APNG with `comf` chunks (`key\0json`) after the frames |
| `comfyui.avif` | 2,495 | `SaveImageAdvanced`-style AVIF through Pillow/libavif: an `Exif` item with Make `workflow:` / Model `prompt:` |
| `comfyui.mp4` | 2,417 | VideoHelperSuite / FFmpeg `-movflags use_metadata_tags`: mdta keys, moov after mdat |
| `comfyui.webm` | 2,215 | VideoHelperSuite WebM: Matroska SimpleTags `PROMPT` / `WORKFLOW` (no frames) |
| `comfyui_audio.flac` | 2,137 | `SaveAudio` FLAC: Vorbis comments `prompt` / `workflow` (no frames) |
| `comfyui_audio.mp3` | 2,425 | `SaveAudio` MP3: ID3v2.4 TXXX `prompt` / `workflow` + silent frames |
| `comfyui.exr` | 2,434 | EXR save nodes: string header attributes `prompt` / `workflow` |
| `comfyui_big_workflow.png` | 2,033,856 | a 2 MB UI workflow for the large-payload read and parse benchmarks |

## InvokeAI

| File | Bytes | Mimics |
|---|---:|---|
| `invokeai.png` | 1,606 | InvokeAI >= 3 `image_files_disk.py`: tEXt `invokeai_metadata`, `invokeai_workflow`, `invokeai_graph` |
| `invokeai_graph_only.png` | 917 | InvokeAI output that carries only `invokeai_graph` (no metadata) |
| `invokeai_legacy.png` | 1,281 | InvokeAI <= 2.3 `pngwriter.py`: tEXt `Dream` (command line) + `sd-metadata` (JSON) |

## NovelAI

| File | Bytes | Mimics |
|---|---:|---|
| `novelai.png` | 1,922 | NovelAI: tEXt `Title`, `Description`, `Software`, `Source`, `Generation time`, `Comment` (JSON) |
| `novelai_stealth_only.png` | 32,964 | NovelAI stealth data only (alpha LSB, gzip JSON; ExifTool cannot read it) |

## Fooocus family

| File | Bytes | Mimics |
|---|---:|---|
| `fooocus.png` | 1,574 | Fooocus `meta_parser.py`: tEXt `parameters` (JSON, ASCII) + `fooocus_scheme` |
| `fooocus_a1111.png` | 1,474 | Fooocus with the a1111 scheme: infotext with `Raw prompt`, `Version: Fooocus v...` |
| `fooocus.jpg` | 1,378 | Fooocus JPEG: Pillow EXIF IFD0 `UserComment` (no charset header), `Software`, MakerNote = scheme |
| `fooocus.webp` | 828 | Fooocus WebP: the same Pillow EXIF |
| `fooocus_mre.png` | 1,429 | Fooocus-MRE `private_logger.py`: tEXt `Comment` JSON (`ensure_ascii=False`) |
| `fooocus_mre.jpg` | 1,199 | Fooocus-MRE JPEG: the same JSON in a COM segment |
| `ruinedfooocus.png` | 1,309 | RuinedFooocus `async_worker.py`: tEXt `parameters` flat JSON, `software: RuinedFooocus` |

## SwarmUI, Easy Diffusion, Draw Things, Midjourney, Tensor.art, character cards

| File | Bytes | Mimics |
|---|---:|---|
| `swarmui.png` | 1,824 | SwarmUI `ImageFile.cs`: tEXt `parameters`, indented ASCII-escaped `sui_image_params` JSON |
| `swarmui.jpg` | 2,594 | SwarmUI JPEG (ImageSharp): ExifIFD `UserComment` UNICODE + UTF-16LE in a little-endian TIFF |
| `easydiffusion.png` | 1,201 | Easy Diffusion `file_utils.py`: one tEXt per field (snake_case keys, `str()` values) |
| `easydiffusion.jpg` | 1,266 | Easy Diffusion JPEG: `piexif.insert`, `UserComment` UNICODE JSON |
| `drawthings.png` | 2,214 | Draw Things: XMP in iTXt, `exif:UserComment` JSON + `dc:description` + `xmp:CreatorTool` |
| `midjourney.png` | 1,614 | Midjourney web PNG: tEXt `Creation Time`, `Author`, `Description` (`--flags`, `Job ID:`) + XMP `DigImageGUID`, `https` DigitalSourceType |
| `tensorart.png` | 2,183 | Tensor.art: tEXt `generation_data` JSON with a trailing NUL, plus a ComfyUI `prompt` with custom loader nodes |
| `charactercard.png` | 1,295 | LLM character card: tEXt `chara` (base64 JSON, v2) + `ccv3` (v3) |

## Standards (IPTC, GB 45438-2025 AIGC, AI-training signals)

| File | Bytes | Mimics |
|---|---:|---|
| `iptc_ai_fields.jpg` | 3,911 | IPTC 2025.1: `DigitalSourceType` + `AISystemUsed`, `AISystemVersionUsed`, `AIPromptInformation`, `AIPromptWriterName` (ExifTool-written) |
| `meta_ai_like.jpg` | 3,578 | Meta AI: only XMP `DigitalSourceType` = trainedAlgorithmicMedia (ExifTool-written) |
| `google_photos_ai_edit.jpg` | 3,760 | Google Photos AI edit: `XMP-photoshop:Credit` = Edited with Google AI + compositeWithTrainedAlgorithmicMedia (ExifTool-written) |
| `ai_training_signals.jpg` | 3,687 | AI-training signals: `XMP-plus:DataMining` + Exif 3.1 `LearningOptOutIn` (ExifTool-written) |
| `aigc.png` | 1,117 | GB 45438-2025 Annex B: tEXt `AIGC` with the wrapped JSON `{"AIGC": {...}}` |
| `aigc_xmp.jpg` | 1,364 | TC260-PG-20259A: XMP `TC260:AIGC` (namespace `http://www.tc260.org.cn/ns/AIGC/1.0/`) with the flat JSON |
| `aigc.mp4` | 522 | TC260-PG-20257A: MP4 `moov/udta/meta` mdta key `AIGC` with the flat JSON (written with exifglass.config) (ExifTool-written) |

## C2PA, synthetic store (unsigned, filler hashes; exact v1 claim_generator string)

| File | Bytes | Mimics |
|---|---:|---|
| `c2pa_synthetic.jpg` | 4,714 | JPEG APP11 JUMBF: a v1 Firefly-like parent + an active `c2pa.claim.v2` manifest |
| `c2pa_synthetic.png` | 4,932 | PNG `caBX` chunk before IDAT |
| `c2pa_synthetic.webp` | 4,216 | WebP `C2PA` chunk after EXIF |
| `c2pa_synthetic.avif` | 4,431 | AVIF with a top-level C2PA `uuid` box |
| `c2pa_synthetic.mp4` | 4,277 | MP4 with a top-level C2PA `uuid` box |

## C2PA, signed with c2patool 0.27.22 (c2pa-rs 0.90) and its test certificate; not reproducible byte for byte

| File | Bytes | Mimics |
|---|---:|---|
| `c2pa_chatgpt.png` | ~29,269 | ChatGPT (2025): v2; ingredient `ChatGPT` with `c2pa.created` by `GPT-4o` + trainedAlgorithmicMedia and a bare `c2pa.converted`; active `ChatGPT` with `c2pa.opened` |
| `c2pa_openai_api.png` | ~14,412 | OpenAI API (2026): v2, one manifest `OpenAI Media Service API`, `gpt-image` 2.0, converted, `c2pa.watermarked.unbound` |
| `c2pa_dalle3.webp` | ~27,428 | DALL-E 3 in ChatGPT (2024): v1 claims; ingredient `OpenAI-API` with `c2pa.created` by `DALL·E`; active `ChatGPT` |
| `c2pa_firefly.jpg` | ~14,245 | Adobe Firefly (2025): v1 claim `Adobe_Firefly`, `c2pa.created` by `Adobe Firefly` (string), firefly parameters, trainedAlgorithmicMedia |
| `c2pa_photoshop_genfill.jpg` | ~29,226 | Photoshop Generative Fill (values inferred): v2 `Adobe Photoshop`, camera ingredient, `c2pa.edited` by `Adobe Firefly` with compositeWithTrainedAlgorithmicMedia |
| `c2pa_gemini.png` | ~30,419 | Gemini app: v2; ingredient `Google C2PA Core Generator Library` (created + SynthID, trainedAlgorithmicMedia), active wrapper (opened, watermark, converted); XMP Credit `Made with Google AI` + DigitalSourceType |
| `c2pa_microsoft.png` | ~14,353 | Microsoft Designer / Azure OpenAI: v1 `Microsoft Responsible AI Image Provenance`, agent `Azure OpenAI DALL-E`, description `AI Generated Image`, no DigitalSourceType |
| `c2pa_sora.mp4` | ~14,418 | Sora MP4: v2, one manifest `Sora`, `c2pa.created` by `Sora` + trainedAlgorithmicMedia |
| `c2pa_capture.jpg` | ~14,090 | not AI: a camera-like `c2pa.created` with digitalCapture (provenance only, must not be flagged as AI) |

## Not AI (no false positives)

| File | Bytes | Mimics |
|---|---:|---|
| `negative_camera.jpg` | 4,230 | camera JPEG: Make/Model/Software, a UserComment, XMP DigitalSourceType digitalCapture (ExifTool-written) |
| `negative_text.png` | 988 | PNG with ordinary text chunks: `Software` GIMP, `Comment`, a `Description` that mentions prompts |
