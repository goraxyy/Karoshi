using System.IO;
using System.Linq;
using Kehai;
using NUnit.Framework;

// The first launch as Kehai copies the save data the game kept as Karoshi: the right files,
// under the right names, only once, and never touching the old folder.
public class KehaiMigrationTests
{
    string root, before, now;

    [SetUp]
    public void MakeFolders()
    {
        root = Path.Combine(Path.GetTempPath(), "kehai_migration_" + System.Guid.NewGuid().ToString("N"));
        before = Path.Combine(root, "Karoshi");
        now = Path.Combine(root, "Kehai");
        Directory.CreateDirectory(before);
    }

    [TearDown]
    public void RemoveFolders()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    void Write(string relative, string text)
    {
        string path = Path.Combine(before, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
    }

    void OldSave()
    {
        Write("karen_ledger.json", "{\"version\":1,\"shiftsWorked\":3}");
        Write("karen_logs/shift_01.jsonl", "{\"kind\":\"PLAN\"}");
        Write("karoshi_eval/ablation_20260925.jsonl", "{\"rung\":\"F\",\"karen_catches\":2,\"karen_top_tactics\":\"spill fog\"}\n{\"karen_catches\":0}");
        Write("karoshi_eval/eval_ledger.json", "{\"version\":1}");
        Write("shift_records/shift_01_20260926_054221.json", "{\"shift\":1}");
        Write("Unity/cache.bin", "Unity's own");
        Write("TestResults.xml", "<test-run/>");
    }

    [Test]
    public void CopiesEachKindUnderItsNewName()
    {
        OldSave();
        Assert.AreEqual(4, KehaiMigration.CopyData(before, now));

        Assert.AreEqual("{\"version\":1,\"shiftsWorked\":3}", File.ReadAllText(Path.Combine(now, "aiko_ledger.json")));
        Assert.IsTrue(File.Exists(Path.Combine(now, "aiko_logs", "shift_01.jsonl")));
        Assert.IsTrue(File.Exists(Path.Combine(now, "kehai_eval", "eval_ledger.json")));
        Assert.IsTrue(File.Exists(Path.Combine(now, "shift_records", "shift_01_20260926_054221.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(now, "Unity")), "Unity's own cache isn't the game's data");
        Assert.IsFalse(File.Exists(Path.Combine(now, "TestResults.xml")));

        string eval = File.ReadAllText(Path.Combine(now, "kehai_eval", "ablation_20260925.jsonl"));
        Assert.AreEqual("{\"rung\":\"F\",\"aiko_catches\":2,\"aiko_top_tactics\":\"spill fog\"}\n{\"aiko_catches\":0}", eval);
    }

    [Test]
    public void LeavesTheOldFolderAsItWas()
    {
        OldSave();
        string[] Listing() => Directory.GetFiles(before, "*", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
        string[] was = Listing();
        string evalWas = File.ReadAllText(Path.Combine(before, "karoshi_eval", "ablation_20260925.jsonl"));

        KehaiMigration.CopyData(before, now);

        CollectionAssert.AreEqual(was, Listing());
        Assert.AreEqual(evalWas, File.ReadAllText(Path.Combine(before, "karoshi_eval", "ablation_20260925.jsonl")));
    }

    [Test]
    public void DoesNothingWhenTheNewFolderAlreadyHasData()
    {
        OldSave();
        Directory.CreateDirectory(Path.Combine(now, "shift_records"));
        File.WriteAllText(Path.Combine(now, "shift_records", "shift_02.json"), "{}");

        Assert.AreEqual(0, KehaiMigration.CopyData(before, now));
        Assert.IsFalse(File.Exists(Path.Combine(now, "aiko_ledger.json")));
    }

    [Test]
    public void AnEmptyFolderIsNotData()
    {
        OldSave();
        Directory.CreateDirectory(Path.Combine(now, "kehai_eval"));   // e.g. "Open Eval Folder" ran first

        Assert.IsFalse(KehaiMigration.HasData(now));
        Assert.AreEqual(4, KehaiMigration.CopyData(before, now));
    }

    [Test]
    public void RunsOnlyOnce()
    {
        OldSave();
        KehaiMigration.CopyData(before, now);
        File.WriteAllText(Path.Combine(now, "aiko_ledger.json"), "{\"version\":1,\"shiftsWorked\":4}");

        Assert.AreEqual(0, KehaiMigration.CopyData(before, now));
        Assert.AreEqual("{\"version\":1,\"shiftsWorked\":4}", File.ReadAllText(Path.Combine(now, "aiko_ledger.json")));
    }

    [Test]
    public void NoOldFolderOrTheSameFolderIsANoOp()
    {
        Assert.AreEqual(0, KehaiMigration.CopyData(Path.Combine(root, "nothing here"), now));
        OldSave();
        Assert.AreEqual(0, KehaiMigration.CopyData(before, before));
    }

    [Test]
    public void TakesDataTheRenamedGameWroteBeforeTheProductNameChanged()
    {
        Write("aiko_ledger.json", "{\"version\":1,\"shiftsWorked\":5}");
        Write("karen_ledger.json", "{\"version\":1,\"shiftsWorked\":3}");

        KehaiMigration.CopyData(before, now);
        Assert.AreEqual("{\"version\":1,\"shiftsWorked\":5}", File.ReadAllText(Path.Combine(now, "aiko_ledger.json")));
    }

    // The shape `plutil -convert xml1` prints for the game's old PlayerPrefs.
    const string OldPlist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>Karoshi.KarenCone</key>
	<integer>0</integer>
	<key>Karoshi.MouseSensitivity</key>
	<real>1.5</real>
	<key>Karoshi.Volume.Karen</key>
	<real>0.75</real>
	<key>UnityGraphicsQuality</key>
	<integer>0</integer>
	<key>karen.blink.cal.vision.closed</key>
	<real>0.28540000319480896</real>
	<key>karen.blink.consent</key>
	<integer>1</integer>
	<key>unity.cloud_userid</key>
	<string>76534852abee34262b3f3bd908b3fe06</string>
</dict>
</plist>";

    [Test]
    public void PrefsKeepTheirTypesUnderTheNewNames()
    {
        var prefs = KehaiMigration.RenamePrefs(KehaiMigration.ParsePlist(OldPlist)).ToDictionary(p => p.Key);

        CollectionAssert.AreEquivalent(new[] { "Kehai.AikoCone", "Kehai.MouseSensitivity", "Kehai.Volume.Aiko", "aiko.blink.cal.vision.closed", "aiko.blink.consent" }, prefs.Keys);
        Assert.AreEqual(KehaiMigration.PrefKind.Int, prefs["Kehai.AikoCone"].Kind);
        Assert.AreEqual(0, prefs["Kehai.AikoCone"].Int);
        Assert.AreEqual(KehaiMigration.PrefKind.Float, prefs["Kehai.Volume.Aiko"].Kind);
        Assert.AreEqual(0.75f, prefs["Kehai.Volume.Aiko"].Float);
        Assert.AreEqual(0.2854f, prefs["aiko.blink.cal.vision.closed"].Float, 1e-6f);
        Assert.AreEqual(1, prefs["aiko.blink.consent"].Int);
    }

    [Test]
    public void AnUnreadablePlistMeansNoPrefs()
    {
        Assert.IsEmpty(KehaiMigration.ParsePlist(""));
        Assert.IsEmpty(KehaiMigration.ParsePlist("<plist version=\"1.0\"><array/></plist>"));
    }
}
