using NetTopologySuite.Geometries;
using PLATEAU.Snap.Models.Client;
using PLATEAU.Snap.Models.Exceptions;
using PLATEAU.Snap.Models.Settings;
using PLATEAU.Snap.Server.Entities.Models;
using PLATEAU.Snap.Server.Repositories;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml;

namespace PLATEAU.Snap.Server.Services;

internal class CityDbService : ICityDbService
{
    private readonly IImageRepository imageRepository;

    private readonly IImageProcessingService imageProcessingService;

    private readonly ISurfaceGeometryRepository surfaceGeometryRepository;

    private readonly AppSettings appSettings;

    private readonly DatabaseSettings databaseSettings;

    public CityDbService(IImageRepository imageRepository, IImageProcessingService imageProcessingService, ISurfaceGeometryRepository surfaceGeometryRepository, AppSettings appSettings, DatabaseSettings databaseSettings)
    {
        this.imageRepository = imageRepository;
        this.imageProcessingService = imageProcessingService;
        this.surfaceGeometryRepository = surfaceGeometryRepository;
        this.appSettings = appSettings;
        this.databaseSettings = databaseSettings;
    }

    public async Task<Stream> ExportAsync(int id)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            // メッシュコードの取得
            var footprint = await this.surfaceGeometryRepository.GetFootprintAsync(id);
            if (footprint is null)
            {
                throw new NotFoundException($"Building with ID {id} does not exist.");
            }
            var meshCode = MeshUtil.GetThirdMeshCode(footprint);

            // 3D City Database Importer/Exporter に不具合があり、zip出力するとzipを閉じる際に例外がスローされることがある
            // これを回避するために、ツールではファイルとして出力し、自前でzip化している
            var outputDirectory = Path.Combine(tempDirectory, "bldg");
            var filePath = Path.Combine(outputDirectory, $"{id}.gml");
            Directory.CreateDirectory(outputDirectory!);
            var arguments = $"export -H {databaseSettings.Host} -P {databaseSettings.Port} -u {databaseSettings.Username} -p {databaseSettings.Password} -d {databaseSettings.Database} --db-id {id} -o {filePath}";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = appSettings.ImportExportToolPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Export failed with exit code {process.ExitCode}. Output: {output}, Error: {error}");
            }

            // PLATEAU の CityGML と同じ形式に変換する
            var plateauGmlPath = Path.Combine(outputDirectory, $"{meshCode}_bldg_6697_2_op.gml");
            var appearanceDirectoryName = $"{meshCode}_bldg_6697_appearance";
            await ApplyPlateauCityGmlAsync(filePath, plateauGmlPath, id, appearanceDirectoryName);
            File.Delete(filePath);

            // appearance フォルダを PLATEAU と同じ形式に変更
            var sourceAppearancePath = Path.Combine(outputDirectory, "appearance");
            var plateauAppearancePath = Path.Combine(outputDirectory, appearanceDirectoryName);
            if (Directory.Exists(sourceAppearancePath))
            {
                Directory.Move(sourceAppearancePath, Path.Combine(tempDirectory, plateauAppearancePath));
            }

            var zipFilePath = Path.ChangeExtension(tempDirectory, $"{meshCode}.zip");
            ZipFile.CreateFromDirectory(outputDirectory, zipFilePath, CompressionLevel.Optimal, false);

            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(memoryStream);
            }
            memoryStream.Position = 0;

            return memoryStream;
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    public async Task<PreviewTextureResponse> PreviewTextureRequest(PreviewTextureRequest payload)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("PreviewTextureRequest is not implemented yet.");
    }

    public async Task ApplyTextureAsync(ApplyTextureRequest payload)
    {
        if (!(await this.imageRepository.FaceExists(payload.FaceId)))
        {
            throw new NotFoundException($"Face with ID {payload.FaceId} does not exist.");
        }

        var textureparam = await this.imageRepository.GetTextureparamAsync(payload.FaceId);

        var response = await imageProcessingService.ApplyTextureAsync(new Models.Lambda.LambdaApplyTextureRequest()
        {
            Path = payload.Path,
            Coordinates = payload.Coordinates,
        });

        var reader = new NetTopologySuite.IO.WKTReader();
        var textureCoordinates = reader.Read(response.TextureCoordinates) as Polygon ?? throw new InvalidOperationException($"{nameof(response.TextureCoordinates)} is not a valid polygon.");
        var fileBytes = await this.imageRepository.DownloadAsync(response.Path);
        string? imageType = null;
        switch (Path.GetExtension(response.Path).ToLower())
        {
            case ".png":
                imageType = "png";
                break;
            case ".jpg":
            case ".jpeg":
                imageType = "jpg";
                break;
            default:
                throw new InvalidOperationException($"Unsupported texture file type: {Path.GetExtension(response.Path)}");
        }

        if (textureparam is null)
        {
            // Textureparamが存在しない場合は新規追加
            await AddTextureparamAsync(payload.FaceId, textureCoordinates, imageType, fileBytes, payload.BuildingId);
        }
        else
        {
            if (textureparam.SurfaceData.TexImage is null)
            {
                throw new InvalidOperationException("SurfaceData is null in Textureparam.");
            }
            var count = await this.imageRepository.CountSurfaceData(textureparam.SurfaceData.TexImage.Id);

            if (count == 1)
            {
                // このTexImageに紐づくSurfaceDataが1つだけの場合は更新
                await UpdateTexImageAsync(textureparam, textureCoordinates, imageType, fileBytes, payload.BuildingId);
            }
            else if (count > 1)
            {
                // このTexImageに紐づくSurfaceDataが複数ある場合は新規追加
                await AddTexImageAsync(textureparam, textureCoordinates, imageType, fileBytes, payload.BuildingId);
            }
            else
            {
                throw new InvalidOperationException("No SurfaceData found for the given TexImageId.");
            }
        }
    }

    private async Task AddTextureparamAsync(int faceId, Polygon textureCoordinates, string imageType, byte[] fileBytes, int buildingId)
    {
        var objectClass = await this.imageRepository.GetObjectClass("ParameterizedTexture");
        if (objectClass is null)
        {
            throw new InvalidOperationException("Object class 'ParameterizedTexture' not found.");
        }

        var appearance = await this.imageRepository.GetAppearanceAsync(buildingId);
        if (appearance is null)
        {
            throw new InvalidOperationException($"Building with ID {buildingId} does not exist.");
        }

        var textureparam = new Textureparam
        {
            SurfaceGeometryId = faceId,
            IsTextureParametrization = 1,
            TextureCoordinates = textureCoordinates,
            SurfaceData = new SurfaceDatum
            {
                Gmlid = $"ID_{Guid.NewGuid()}",
                IsFront = 1,
                ObjectclassId = objectClass.Id,
                TexImage = new TexImage
                {
                    TexMimeType = $"image/{imageType}",
                    TexImageData = fileBytes,
                    TexImageUri = $"tex_{Guid.NewGuid()}.{imageType}",
                },
                Appearances = new List<Appearance> { appearance }
            }
        };

        await this.imageRepository.AddTextureparamAsync(textureparam);
    }

    private async Task UpdateTexImageAsync(Textureparam textureparam, Polygon textureCoordinates, string imageType, byte[] fileBytes, int buildingId)
    {
        if (textureparam.SurfaceData.TexImage is null)
        {
            throw new InvalidOperationException("TexImage is null in Textureparam.");
        }

        textureparam.TextureCoordinates = textureCoordinates;
        textureparam.SurfaceData.TexImage.TexMimeType = $"image/{imageType}";
        textureparam.SurfaceData.TexImage.TexImageData = fileBytes;
        textureparam.SurfaceData.TexImage.TexImageUri = !string.IsNullOrEmpty(textureparam.SurfaceData.TexImage.TexImageUri)
            ? Path.ChangeExtension(textureparam.SurfaceData.TexImage.TexImageUri, $".{imageType}")
            : $"tex_{textureparam.SurfaceData.TexImage.Id}.{imageType}";

        var isAppearancesModified = false;
        if (textureparam.SurfaceData.Appearances is null || textureparam.SurfaceData.Appearances.Count == 0)
        {
            var appearance = await this.imageRepository.GetAppearanceAsync(buildingId);
            if (appearance is null)
            {
                throw new InvalidOperationException($"Building with ID {buildingId} does not exist.");
            }

            textureparam.SurfaceData.Appearances = new List<Appearance> { appearance };
            isAppearancesModified = true;
        }

        await this.imageRepository.UpdateTextureparamAsync(textureparam, isAppearancesModified);
    }

    private async Task AddTexImageAsync(Textureparam textureparam, Polygon textureCoordinates, string imageType, byte[] fileBytes, int buildingId)
    {
        var appearance = await this.imageRepository.GetAppearanceAsync(buildingId);
        if (appearance is null)
        {
            throw new InvalidOperationException($"Building with ID {buildingId} does not exist.");
        }

        var surfaceData = new SurfaceDatum
        {
            Gmlid = $"ID_{Guid.NewGuid()}",
            IsFront = 1,
            ObjectclassId = textureparam.SurfaceData.ObjectclassId,
            TexImage = new TexImage
            {
                TexMimeType = $"image/{imageType}",
                TexImageData = fileBytes,
                TexImageUri = $"tex_{Guid.NewGuid()}.{imageType}",
            },
            Appearances = new List<Appearance> { appearance }
        };
        textureparam.SurfaceData = surfaceData;
        await this.imageRepository.UpdateTextureparamAsync(textureparam, true);
    }

    private async Task<string> ApplyPlateauCityGmlAsync(string inputPath, string outputPath, int buildingId, string plateauAppearancePath)
    {
        var envelope = await this.surfaceGeometryRepository.GetEnvelopeGeometryAsync(buildingId);
        if (envelope is null)
        {
            throw new InvalidOperationException($"Building with ID {buildingId} does not exist.");
        }

        ApplyPlateauCityGml(inputPath, outputPath, envelope, plateauAppearancePath);

        return outputPath;
    }

    private static void ApplyPlateauCityGml(string inputPath, string outputPath, Geometry envelope, string plateauAppearancePath)
    {
        using (XmlReader reader = XmlReader.Create(inputPath))
        using (XmlWriter writer = XmlWriter.Create(outputPath, new XmlWriterSettings { Indent = true }))
        {
            bool insideCityModel = false;
            bool boundedByWritten = false;
            bool isImageUri = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "CityModel")
                {
                    // Write CityModel start tag with attributes
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    if (reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                        {
                            writer.WriteAttributeString(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value);
                        }
                        reader.MoveToElement(); // Move back to the element node
                    }

                    // フラグ立ててboundedBy挿入タイミングとする
                    insideCityModel = true;
                    boundedByWritten = false;
                }
                else if (insideCityModel && !boundedByWritten)
                {
                    // 最初の子ノードの前にboundedByを挿入
                    WriteBoundedBy(writer, envelope);
                    boundedByWritten = true;

                    // 先読みしたノードを書き戻す
                    WriteShallowNode(reader, writer, isImageUri, plateauAppearancePath);
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "imageURI")
                {
                    isImageUri = true;
                    WriteShallowNode(reader, writer, isImageUri, plateauAppearancePath);
                }
                else
                {
                    // 通常通り書き出し
                    WriteShallowNode(reader, writer, isImageUri, plateauAppearancePath);
                }

                // CityModelの終了タグに達したらリセット
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "CityModel")
                {
                    insideCityModel = false;
                    boundedByWritten = false;
                }

                // imageURIの終了タグに達したらリセット
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "imageURI")
                {
                    isImageUri = false;
                }
            }
        }
    }

    private static void WriteBoundedBy(XmlWriter writer, Geometry envelope)
    {
        var coordinates = envelope.Coordinates;
        writer.WriteStartElement("gml", "boundedBy", "http://www.opengis.net/gml");

        writer.WriteStartElement("gml", "Envelope", "http://www.opengis.net/gml");
        writer.WriteAttributeString("srsName", "http://www.opengis.net/def/crs/EPSG/0/6697");
        writer.WriteAttributeString("srsDimension", "3");

        writer.WriteElementString("gml", "lowerCorner", "http://www.opengis.net/gml",
            $"{coordinates.Min(x => x.Y)} {coordinates.Min(x => x.X)} {coordinates.Min(x => x.Z)}");
        writer.WriteElementString("gml", "upperCorner", "http://www.opengis.net/gml",
            $"{coordinates.Max(x => x.Y)} {coordinates.Max(x => x.X)} {coordinates.Max(x => x.Z)}");

        writer.WriteEndElement(); // Envelope
        writer.WriteEndElement(); // boundedBy
    }

    private static void WriteShallowNode(XmlReader reader, XmlWriter writer, bool isImageUri, string plateauAppearancePath)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                if (reader.HasAttributes)
                {
                    while (reader.MoveToNextAttribute())
                        writer.WriteAttributeString(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value);
                    reader.MoveToElement();
                }

                if (reader.IsEmptyElement)
                    writer.WriteEndElement();
                break;

            case XmlNodeType.Text:
                writer.WriteString(!isImageUri ? reader.Value : reader.Value.Replace("appearance/", $"{plateauAppearancePath}/"));
                break;

            case XmlNodeType.CDATA:
                writer.WriteCData(reader.Value);
                break;

            case XmlNodeType.ProcessingInstruction:
            case XmlNodeType.XmlDeclaration:
                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                break;

            case XmlNodeType.Comment:
                writer.WriteComment(reader.Value);
                break;

            case XmlNodeType.EndElement:
                writer.WriteEndElement();
                break;

            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                writer.WriteWhitespace(reader.Value);
                break;
        }
    }
}
