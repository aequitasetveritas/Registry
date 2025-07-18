using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Registry.Adapters.DroneDB;
using Registry.Common;
using Registry.Common.Model;
using Registry.Common.Test;
using Registry.Ports;
using Registry.Ports.DroneDB;
using Registry.Test.Common;

namespace Registry.Adapters.Ddb.Test;

[TestFixture]
public class NativeDdbWrapperTests : TestBase
{
    private const string BaseTestFolder = nameof(NativeDdbWrapperTests);
    private const string TestFileUrl =
        "https://github.com/DroneDB/test_data/raw/master/test-datasets/drone_dataset_brighton_beach/DJI_0023.JPG";
    private const string Test1ArchiveUrl = "https://github.com/DroneDB/test_data/raw/master/registry/DdbFactoryTest/testdb1.zip";
    private const string Test3ArchiveUrl = "https://github.com/DroneDB/test_data/raw/master/ddb-test/Test3.zip";

    private const string DdbFolder = ".ddb";

    private const string TestGeoTiffUrl =
        "https://github.com/DroneDB/test_data/raw/master/brighton/odm_orthophoto.tif";

    private const string TestDelta1ArchiveUrl = "https://github.com/DroneDB/test_data/raw/master/delta/first.zip";
    private const string TestDelta2ArchiveUrl = "https://github.com/DroneDB/test_data/raw/master/delta/second.zip";

    private const string TestPointCloudUrl =
        "https://github.com/DroneDB/test_data/raw/master/brighton/point_cloud.laz";

    private static readonly IDdbWrapper DdbWrapper = new NativeDdbWrapper(true);

    [OneTimeSetUp]
    public void Setup()
    {

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);

            var ddbFolder = CommonUtils.FindDdbFolder();
            if (ddbFolder == null)
                throw new Exception("DDB not found");

            CommonUtils.SetDefaultDllPath(ddbFolder);
        }


        DdbWrapper.RegisterProcess(true);
    }

    [Test]
    public void GetVersion_HasValue()
    {
        DdbWrapper.GetVersion().Length.Should().BeGreaterThan(0, "Can call GetVersion()");
    }

    [Test]
    public void Init_NonExistant_Exception()
    {
        Action act = () => DdbWrapper.Init("nonexistant");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Init(null);
        act.Should().Throw<DdbException>();

    }

    [Test]
    public void Init_EmptyFolder_Ok()
    {

        using var area = new TestArea(nameof(Init_EmptyFolder_Ok));
            
        DdbWrapper.Init(area.TestFolder).Should().Contain(area.TestFolder);
        Directory.Exists(Path.Join(area.TestFolder, ".ddb")).Should().BeTrue();
    }

    [Test]
    public void Add_NonExistant_Exception()
    {
        Action act = () => DdbWrapper.Add("nonexistant", "");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Add("nonexistant", "test");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Add(null, "test");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Add("nonexistant", (string)null);
        act.Should().Throw<DdbException>();

    }

    [Test]
    public void EndToEnd_Add_Remove()
    {

        using var area = new TestArea(nameof(EndToEnd_Add_Remove));

        DdbWrapper.Init(area.TestFolder);

        File.WriteAllText(Path.Join(area.TestFolder, "file.txt"), "test");
        File.WriteAllText(Path.Join(area.TestFolder, "file2.txt"), "test");
        File.WriteAllText(Path.Join(area.TestFolder, "file3.txt"), "test");

        Assert.Throws<DdbException>(() => DdbWrapper.Add(area.TestFolder, "invalid"));

        var entry = DdbWrapper.Add(area.TestFolder, Path.Join(area.TestFolder, "file.txt"))[0];
        entry.Path.Should().Be("file.txt");
        entry.Hash.Should().NotBeNullOrWhiteSpace();

        var entries = DdbWrapper.Add(area.TestFolder, [Path.Join(area.TestFolder, "file2.txt"), Path.Join(area.TestFolder, "file3.txt")
        ]);
        entries.Should().HaveCount(2);

        DdbWrapper.Remove(area.TestFolder, Path.Combine(area.TestFolder, "file.txt"));

    }

    [Test]
    public void Info_GenericFile_Details()
    {

        using var area = new TestArea(nameof(Info_GenericFile_Details));

        File.WriteAllText(Path.Join(area.TestFolder, "file.txt"), "test");
        File.WriteAllText(Path.Join(area.TestFolder, "file2.txt"), "test");

        var e = DdbWrapper.Info(Path.Join(area.TestFolder, "file.txt"), withHash: true)[0];
        e.Hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");

        var es = DdbWrapper.Info(area.TestFolder, true);
        es.Count.Should().Be(2);

        es[0].Type.Should().Be(EntryType.Generic);
        es[0].Size.Should().BeGreaterThan(0);
        es[0].ModifiedTime.Year.Should().Be(DateTime.Now.Year);
    }

    [Test]
    public void Add_ImageFile_Ok()
    {

        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        using var tempFile = new TempFile(TestFileUrl, BaseTestFolder);

        DdbWrapper.Remove(ddbPath, Path.Combine(ddbPath, "DJI_0023.JPG"));

        var destPath = Path.Combine(ddbPath, Path.GetFileName(tempFile.FilePath));

        File.Move(tempFile.FilePath, destPath);

        var res = DdbWrapper.Add(ddbPath, destPath);

        res.Count.Should().Be(1);

    }

    [Test]
    public void Info_ImageFile_Details()
    {

        //var expectedMeta = JsonConvert.DeserializeObject<Dictionary<string, string>>(
        //    @"{""cameraPitch"":""-89.9000015258789"",""cameraRoll"":""0.0"",""cameraYaw"":""43.79999923706055"",""captureTime"":""1466699554000.0"",""focalLength"":""3.4222222222222225"",""focalLength35"":""20.0"",""height"":""2250"",""make"":""DJI"",""model"":""FC300S"",""orientation"":""1"",""sensor"":""dji fc300s"",""sensorHeight"":""3.4650000000000003"",""sensorWidth"":""6.16"",""width"":""4000""}");

        using var tempFile = new TempFile(TestFileUrl, BaseTestFolder);

        var res = DdbWrapper.Info(tempFile.FilePath, withHash: true);

        res.Should().NotBeNull();
        res.Should().HaveCount(1);

        var info = res.First();

        // Just check some fields
        //info.Meta.Should().BeEquivalentTo(expectedMeta);

        info.Properties.Should().NotBeEmpty();
        info.Properties.Should().HaveCount(14);
        info.Properties["make"].Should().Be("DJI");
        info.Properties["model"].Should().Be("FC300S");
        info.Properties["sensor"].Should().Be("dji fc300s");
        info.Hash.Should().Be("246fed68dec31b17dc6d885cee10a2c08f2f1c68901a8efa132c60bdb770e5ff");
        info.Type.Should().Be(EntryType.GeoImage);
        info.Size.Should().Be(3876862);
        // We can ignore this
        // info.Depth.Should().Be(0);
        info.PointGeometry.Should().NotBeNull();
        info.PolygonGeometry.Should().NotBeNull();

    }

    [Test]
    public void List_Nonexistant_Exception()
    {
        Action act = () => DdbWrapper.List("invalid", "");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.List("invalid", "wefrfwef");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.List(null, "wefrfwef");
        act.Should().Throw<DdbException>();

/*        act = () => DdbWrapper.List("invalid", (string)null);
        act.Should().Throw<DdbException>();
*/
    }

    [Test]
    public void List_ExistingFileSubFolder_Ok()
    {
        using var fs = new TestFS(Test1ArchiveUrl, nameof(NativeDdbWrapperTests));

        const string fileName = "Sub/20200610_144436.jpg";
        const int expectedDepth = 1;
        const int expectedSize = 8248241;
        var expectedType = EntryType.GeoImage;
        const string expectedHash = "f27ddc96daf9aeff3c026de8292681296c3e9d952b647235878c50f2b7b39e94";
        var expectedModifiedTime = new DateTime(2020, 06, 10, 14, 44, 36);
        var expectedMeta = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            "{\"captureTime\":1591800276004.8,\"focalLength\":4.16,\"focalLength35\":26.0,\"height\":3024,\"make\":\"samsung\",\"model\":\"SM-G950F\",\"orientation\":1,\"sensor\":\"samsung sm-g950f\",\"sensorHeight\":4.32,\"sensorWidth\":5.76,\"width\":4032}");
        //const double expectedLatitude = 45.50027;
        //const double expectedLongitude = 10.60667;
        //const double expectedAltitude = 141;


        var ddbPath = Path.Combine(fs.TestFolder, "public", "default");

        var res = DdbWrapper.List(ddbPath, Path.Combine(ddbPath, fileName));

        res.Should().HaveCount(1);

        var file = res.First();

        file.Path.Should().Be(fileName);
        // TODO: Handle different timezones
        file.ModifiedTime.Should().BeCloseTo(expectedModifiedTime, new TimeSpan(6, 0, 0));
        file.Hash.Should().Be(expectedHash);
        file.Depth.Should().Be(expectedDepth);
        file.Size.Should().Be(expectedSize);
        file.Type.Should().Be(expectedType);
        file.Properties.Should().BeEquivalentTo(expectedMeta);
        file.PointGeometry.Should().NotBeNull();
        //file.PointGeometry.Coordinates.Latitude.Should().BeApproximately(expectedLatitude, 0.00001);
        //file.PointGeometry.Coordinates.Longitude.Should().BeApproximately(expectedLongitude, 0.00001);
        //file.PointGeometry.Coordinates.Altitude.Should().Be(expectedAltitude);

    }

    [Test]
    public void List_ExistingFile_Ok()
    {
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var res = DdbWrapper.List(ddbPath, Path.Combine(ddbPath, "DJI_0027.JPG"));

        res.Should().HaveCount(1);
        var entry = res.First();

        Entry expectedEntry = JsonConvert.DeserializeObject<Entry>(
            "{\"depth\":0,\"hash\":\"3157958dd4f2562c8681867dfd6ee5bf70b6e9595b3e3b4b76bbda28342569ed\",\"properties\":{\"cameraPitch\":-89.9000015258789,\"cameraRoll\":0.0,\"cameraYaw\":-131.3000030517578,\"captureTime\":1466699584000.0,\"focalLength\":3.4222222222222225,\"focalLength35\":20.0,\"height\":2250,\"make\":\"DJI\",\"model\":\"FC300S\",\"orientation\":1,\"sensor\":\"dji fc300s\",\"sensorHeight\":3.4650000000000003,\"sensorWidth\":6.16,\"width\":4000},\"mtime\":1491156087,\"path\":\"DJI_0027.JPG\",\"point_geom\":{\"crs\":{\"properties\":{\"name\":\"EPSG:4326\"},\"type\":\"name\"},\"geometry\":{\"coordinates\":[-91.99408299999999,46.84260499999999,198.5099999999999],\"type\":\"Point\"},\"properties\":{},\"type\":\"Feature\"},\"polygon_geom\":{\"crs\":{\"properties\":{\"name\":\"EPSG:4326\"},\"type\":\"name\"},\"geometry\":{\"coordinates\":[[[-91.99397836402999,46.8422402913,158.5099999999999],[-91.99357489543,46.84247729175999,158.5099999999999],[-91.99418894036,46.84296945989999,158.5099999999999],[-91.99459241001999,46.8427324573,158.5099999999999],[-91.99397836402999,46.8422402913,158.5099999999999]]],\"type\":\"Polygon\"},\"properties\":{},\"type\":\"Feature\"},\"size\":3185449,\"type\":3}");

        entry.Should().BeEquivalentTo(expectedEntry);

    }

    [Test]
    public void List_AllFiles_Ok()
    {
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var res = DdbWrapper.List(ddbPath, Path.Combine(ddbPath, "."), true);

        res.Should().HaveCount(26);

        res = DdbWrapper.List(ddbPath, ddbPath, true);

        res.Should().HaveCount(26);

    }


    [Test]
    public void Remove_Nonexistant_Exception()
    {
        var act = () => DdbWrapper.Remove("invalid", "");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Remove("invalid", "wefrfwef");
        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.Remove(null, "wefrfwef");
        act.Should().Throw<DdbException>();

    }


    [Test]
    public void Remove_ExistingFile_Ok()
    {
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        const string fileName = "DJI_0027.JPG";

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var res = DdbWrapper.List(ddbPath, Path.Combine(ddbPath, fileName));
        res.Should().HaveCount(1);

        DdbWrapper.Remove(ddbPath, Path.Combine(ddbPath, fileName));

        res = DdbWrapper.List(ddbPath, Path.Combine(ddbPath, fileName));
        res.Should().HaveCount(0);

    }

    [Test]
    public void Remove_AllFiles_Ok()
    {
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        const string fileName = ".";

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        DdbWrapper.Remove(ddbPath, Path.Combine(ddbPath, fileName));

        var res = DdbWrapper.List(ddbPath, ".", true);
        res.Should().HaveCount(0);

    }

    [Test]
    public void Remove_NonexistantFile_Exception()
    {
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        const string fileName = "elaiuyhrfboeawuyirgfb";

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var act = () => DdbWrapper.Remove(ddbPath, Path.Combine(ddbPath, fileName));

        act.Should().Throw<DdbException>();
    }

    [Test]
    public void Entry_Deserialization_Ok()
    {
        var json = "{'hash': 'abc', 'mtime': 5}";
        var e = JsonConvert.DeserializeObject<Entry>(json);
        e.ModifiedTime.Year.Should().Be(1970);
    }



    [Test]
    public void Password_HappyPath_Ok()
    {

        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        DdbWrapper.VerifyPassword(ddbPath, string.Empty).Should().BeTrue();

        DdbWrapper.AppendPassword(ddbPath, "testpassword");

        DdbWrapper.VerifyPassword(ddbPath, "testpassword").Should().BeTrue();
        DdbWrapper.VerifyPassword(ddbPath, "wrongpassword").Should().BeFalse();

        DdbWrapper.ClearPasswords(ddbPath);
        DdbWrapper.VerifyPassword(ddbPath, "testpassword").Should().BeFalse();


    }

    [Test]
    public void Chaddr_NullAttr_Exception()
    {

        using var test = new TestFS(Test3ArchiveUrl, BaseTestFolder);

        var ddbPath = test.TestFolder;

        Action act = () => DdbWrapper.ChangeAttributes(ddbPath, null);

        act.Should().Throw<ArgumentException>();

    }

    [Test]
    public void GenerateThumbnail_HappyPath_Ok()
    {

        using var tempFile = new TempFile(TestFileUrl, BaseTestFolder);

        var destPath = Path.Combine(Path.GetTempPath(), "test.jpg");//Path.GetTempFileName();

        try
        {
            DdbWrapper.GenerateThumbnail(tempFile.FilePath, 300, destPath);

            var info = new FileInfo(destPath);
            info.Exists.Should().BeTrue();
            info.Length.Should().BeGreaterThan(0);

        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    [Test]
    public void GenerateMemoryThumbnail_HappyPath_Ok()
    {
        using var tempFile = new TempFile(TestFileUrl, BaseTestFolder);
        var buffer = DdbWrapper.GenerateThumbnail(tempFile.FilePath, 300);
        buffer.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void GenerateTile_HappyPath_Ok()
    {

        using var tempFile = new TempFile(TestGeoTiffUrl, BaseTestFolder);

        var destPath = Path.Combine(Path.GetTempPath(), "test.jpg");

        try
        {
            var path = DdbWrapper.GenerateTile(tempFile.FilePath, 18, 64083, 92370, 256, true);
            Debug.WriteLine(path);
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }
    }

    [Test]
    public void GenerateMemoryTile_HappyPath_Ok()
    {
        using var tempFile = new TempFile(TestGeoTiffUrl, BaseTestFolder);

        var buffer = DdbWrapper.GenerateMemoryTile(tempFile.FilePath, 18, 64083, 92370, 256, true);
        buffer.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void Tag_HappyPath_Ok()
    {

        const string goodTag = "pippo/pluto";
        const string goodTagWithRegistry = "https://test.com/pippo/pluto";

        using var test = new TestFS(Test3ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, DdbFolder);

        var tag = DdbWrapper.GetTag(ddbPath);

        tag.Should().BeNull();

        DdbWrapper.SetTag(ddbPath, goodTag);

        tag = DdbWrapper.GetTag(ddbPath);

        tag.Should().Be(goodTag);

        DdbWrapper.SetTag(ddbPath, goodTagWithRegistry);

        tag = DdbWrapper.GetTag(ddbPath);

        tag.Should().Be(goodTagWithRegistry);

    }

    [Test]
    public void Tag_ErrorCases_Ok()
    {

        const string badTag = "pippo";
        const string badTag2 = "����+���+�AAadff_-.-.,";

        using var test = new TestFS(Test3ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, DdbFolder);

        var act = () => DdbWrapper.SetTag(ddbPath, badTag);

        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.SetTag(ddbPath, badTag2);

        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.SetTag(ddbPath, string.Empty);

        act.Should().Throw<DdbException>();

        act = () => DdbWrapper.SetTag(ddbPath, null);

        act.Should().Throw<ArgumentException>();

    }

    [Test]
    public void Stamp_HappyPath_Ok()
    {
        using var test = new TestFS(Test3ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, DdbFolder);

        var stamp = DdbWrapper.GetStamp(ddbPath);
        stamp.Checksum.Should().NotBeNull();
        stamp.Entries.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void Delta_HappyPath_Ok()
    {
        using var source = new TestFS(TestDelta2ArchiveUrl, BaseTestFolder);
        using var destination = new TestFS(TestDelta1ArchiveUrl, BaseTestFolder);

        var delta = DdbWrapper.Delta(source.TestFolder, destination.TestFolder);

        delta.Adds.Length.Should().BeGreaterThan(0);
        delta.Removes.Length.Should().BeGreaterThan(0);

    }

    [Test]
    public void MoveEntry_SimpleRename_Ok()
    {
        using var test = new TestFS(TestDelta2ArchiveUrl, BaseTestFolder);

        DdbWrapper.MoveEntry(test.TestFolder, "plutone.txt", "test.txt");

        var res = DdbWrapper.List(test.TestFolder, test.TestFolder, true);

        res.Should().HaveCount(11);
        res[8].Path.Should().Be("test.txt");
    }

    [Test]
    public void Build_SimpleBuild_Ok()
    {

        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        using var tempFile = new TempFile(TestPointCloudUrl, BaseTestFolder);

        var destPath = Path.Combine(ddbPath, Path.GetFileName(tempFile.FilePath));

        File.Move(tempFile.FilePath, destPath);

        var res = DdbWrapper.Add(ddbPath, destPath);

        res.Count.Should().Be(1);

        DdbWrapper.Build(ddbPath);

    }

    [Test]
    public void IsBuildable_PointCloud_True()
    {

        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        using var tempFile = new TempFile(TestPointCloudUrl, BaseTestFolder);

        var destPath = Path.Combine(ddbPath, Path.GetFileName(tempFile.FilePath));

        File.Move(tempFile.FilePath, destPath);

        var res = DdbWrapper.Add(ddbPath, destPath);

        res.Count.Should().Be(1);

        DdbWrapper.IsBuildable(ddbPath, Path.GetFileName(destPath)).Should().BeTrue();

    }

    [Test]
    public void IsBuildable_TextFile_False()
    {

        using var test = new TestFS(TestDelta2ArchiveUrl, BaseTestFolder);

        DdbWrapper.IsBuildable(test.TestFolder, "lol.txt").Should().BeFalse();

    }

    [Test]
    public void MetaAdd_Ok()
    {
        using var area = new TestArea(nameof(MetaAdd_Ok));
        DdbWrapper.Init(area.TestFolder);

        FluentActions.Invoking(() => DdbWrapper.MetaAdd(area.TestFolder, "test", "123")).Should()
            .Throw<DdbException>(); // Needs plural key
        // DdbWrapper.MetaAdd("metaAddTest", "", "tests", "123").Data.ToObject<int>().Should().Be(123);
    }

    [Test]
    public void MetaAdd_Json()
    {
        using var area = new TestArea(nameof(MetaAdd_Json));
        DdbWrapper.Init(area.TestFolder);

        var res = DdbWrapper.MetaAdd(area.TestFolder, "tests", "{\"test\": true}");
        JsonConvert.SerializeObject(res.Data).Should().Be("{\"test\":true}");
        res.Id.Should().NotBeNull();
        res.ModifiedTime.Should().BeCloseTo(DateTime.UtcNow, new TimeSpan(0,0,3));
    }

    [Test]
    public void MetaSet_Ok()
    {
        using var area = new TestArea(nameof(MetaSet_Ok));
        DdbWrapper.Init(area.TestFolder);

        var f = Path.Join(area.TestFolder, "test.txt");
        File.WriteAllText(f, null);
            
        DdbWrapper.Add(area.TestFolder, f);

        FluentActions.Invoking(() => DdbWrapper.MetaSet(area.TestFolder, "tests", "123", f)).Should()
            .Throw<DdbException>(); // Needs singular key

        DdbWrapper.MetaSet(area.TestFolder, "test", "abc", f).Data.ToObject<string>().Should().Be("abc");
        DdbWrapper.MetaSet(area.TestFolder, "test", "efg", f).Data.ToObject<string>().Should().Be("efg");
    }

    [Test]
    public void MetaRemove_Ok()
    {
        using var area = new TestArea(nameof(MetaRemove_Ok));
        DdbWrapper.Init(area.TestFolder);

        var id = DdbWrapper.MetaSet(area.TestFolder, "test", "123").Id;
        DdbWrapper.MetaRemove(area.TestFolder, "invalid").Should().Be(0);
        DdbWrapper.MetaRemove(area.TestFolder, id).Should().Be(1);
        DdbWrapper.MetaRemove(area.TestFolder, id).Should().Be(0);
    }

    [Test]
    public void MetaGet_Ok()
    {
        using var area = new TestArea(nameof(MetaGet_Ok));
        DdbWrapper.Init(area.TestFolder);

        DdbWrapper.MetaSet(area.TestFolder, "abc", "true");

        FluentActions.Invoking(() => DdbWrapper.MetaGet(area.TestFolder, "nonexistant")).Should()
            .Throw<DdbException>();

        FluentActions.Invoking(() => DdbWrapper.MetaGet(area.TestFolder, "abc", "123")).Should()
            .Throw<DdbException>();

        JsonConvert.DeserializeObject<Meta>(DdbWrapper.MetaGet(area.TestFolder, "abc")).Data.ToObject<bool>()
            .Should().Be(true);
    }

    [Test]
    public void MetaGet_Ok2()
    {
        using var area = new TestArea(nameof(MetaGet_Ok2));
        DdbWrapper.Init(area.TestFolder);

        DdbWrapper.MetaAdd(area.TestFolder, "tests", "{\"test\":true}");
        DdbWrapper.MetaAdd(area.TestFolder, "tests", "{\"test\":false}");
        DdbWrapper.MetaAdd(area.TestFolder, "tests", "{\"test\":null}");

        var res = JsonConvert.DeserializeObject<Meta[]>(DdbWrapper.MetaGet(area.TestFolder, "tests"));

        res.Should().HaveCount(3);

    }

    [Test]
    public void MetaUnset_Ok()
    {
        using var area = new TestArea(nameof(MetaUnset_Ok));
        DdbWrapper.Init(area.TestFolder);
            
        var f = Path.Join(area.TestFolder, "test.txt");
        File.WriteAllText(f, null);

        DdbWrapper.Add(area.TestFolder, f);

        DdbWrapper.MetaSet(area.TestFolder, "abc", "[1,2,3]");
        DdbWrapper.MetaUnset(area.TestFolder, "abc", f).Should().Be(0);
        DdbWrapper.MetaUnset(area.TestFolder, "abc").Should().Be(1);
        DdbWrapper.MetaUnset(area.TestFolder, "abc").Should().Be(0);
    }

    [Test]
    public void MetaList_Ok()
    {
        using var area = new TestArea(nameof(MetaList_Ok));
        DdbWrapper.Init(area.TestFolder);

        DdbWrapper.MetaAdd(area.TestFolder, "annotations", "123");
        DdbWrapper.MetaAdd(area.TestFolder, "examples", "abc");
        DdbWrapper.MetaList(area.TestFolder).Should().HaveCount(2);
    }
        
    [Test]
    public void Stac_Ok()
    {
            
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var res = DdbWrapper.Stac(ddbPath, "DJI_0025.JPG",
            "http://localhost:5000/orgs/public/ds/default", "public/default", "http://localhost:5000");

        res.Should().NotBeNull();
            
        TestContext.WriteLine(res);
    }
        
    [Test]
    public void Stac_NullPath_Ok()
    {
            
        using var test = new TestFS(Test1ArchiveUrl, BaseTestFolder);

        var ddbPath = Path.Combine(test.TestFolder, "public", "default");

        var res = DdbWrapper.Stac(ddbPath, null,
            "http://localhost:5000/orgs/public/ds/default", "public/default", "http://localhost:5000");

        res.Should().NotBeNull();
            
        TestContext.WriteLine(res);

    }
        

    [Test]
    [Explicit("Clean test directory")]
    public void Clean_Domain()
    {
        TempFile.CleanDomain(BaseTestFolder);
        TestFS.ClearCache(BaseTestFolder);
    }
}