using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolClientStartupAuditTests
{
    [Fact]
    public async Task LatestGroupReportsMissingFilesAndNpcErrorDataRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "tqan_fixture.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(
                logPath,
                """
                08:00:00 Load File Error Reason:=Can't Load File Which is old/missing.png
                08:10:00 Can't Load File Which is ini/activity_main.lua
                08:10:01 Load File Error: ini\singleemotion.lua
                08:10:02 Load File Error Reason:=Can't Load File Which is ini/Appkey.dat
                08:10:03 Downloading=ini/activity_main.lua
                08:10:04 error data row=120 file=ini\Npc.ini id=1001 name too long
                08:10:05 error data row=55 file=ini\NpcX.ini empty display name
                08:10:06 error data row=56 file=ini\NpcX.ini empty display name
                """);

            var result = await RunDataToolAsync("client-startup-audit", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client startup audit:", result.StandardOutput);
            Assert.Contains($"Log: {Path.GetFullPath(logPath)}", result.StandardOutput);
            Assert.Contains("Recognized warning lines: 8", result.StandardOutput);
            Assert.Contains("Startup warning groups: 2", result.StandardOutput);
            Assert.Contains("Latest group: 2/2 lines 2-8 time=08:10:00-08:10:06 warnings=7", result.StandardOutput);
            Assert.Contains("Missing files: 3", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua count=1 firstLine=2 lastLine=2", result.StandardOutput);
            Assert.Contains("ini/singleemotion.lua count=1 firstLine=3 lastLine=3", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat count=1 firstLine=4 lastLine=4", result.StandardOutput);
            Assert.Contains("Error data rows: 3", result.StandardOutput);
            Assert.Contains("ini/Npc.ini count=1 firstLine=6 lastLine=6", result.StandardOutput);
            Assert.Contains("ini/NpcX.ini count=2 firstLine=7 lastLine=8", result.StandardOutput);
            Assert.Contains("Download attempts: 1", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua count=1 firstLine=5 lastLine=5", result.StandardOutput);
            Assert.DoesNotContain("old/missing.png count=1", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LatestGroupClassifiesMissingFilesAgainstClientAndReferenceRoots()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-roots-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "tqan_fixture.log");
        var clientRoot = Path.Combine(tempRoot, "client");
        var emptyReferenceRoot = Path.Combine(tempRoot, "reference-empty");
        var donorRoot = Path.Combine(tempRoot, "reference-donor");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            Directory.CreateDirectory(clientRoot);
            Directory.CreateDirectory(emptyReferenceRoot);
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "ini", "activity_main.lua"));
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "ini", "singleemotion.lua"));
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "ini", "luacfg", "activity_main.dat"));
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "ini", "luacfg", "singleemotion.dat"));
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "ini", "Appkey.dat"));
            await File.WriteAllTextAsync(
                logPath,
                """
                08:10:00 Can't Load File Which is ini/activity_main.lua
                08:10:01 Load File Error: ini\singleemotion.lua
                08:10:02 Load File Error Reason:=Can't Load File Which is ini/Appkey.dat
                """);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot,
                "--reference-root",
                $"emptyref={emptyReferenceRoot}",
                $"--reference-root=donor7770={donorRoot}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Latest group: 1/1 lines 1-3 time=08:10:00-08:10:02 warnings=3", result.StandardOutput);
            Assert.Contains("Missing files: 3", result.StandardOutput);
            Assert.Contains("Missing file provenance:", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua clientRootPresent=false references=donor7770", result.StandardOutput);
            Assert.Contains("ini/singleemotion.lua clientRootPresent=false references=donor7770", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat clientRootPresent=false references=donor7770", result.StandardOutput);
            Assert.Contains("clientRootPresent=false references=donor7770", result.StandardOutput);
            Assert.DoesNotContain("clientRootPresent=true", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AllGroupsOptionReportsEarlierGroupsToo()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-all-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "tqan_fixture.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(
                logPath,
                """
                08:00:00 Load File Error Reason:=Can't Load File Which is data2\playskin\default\IMAGE_BACKGROUNDPIC.png
                08:06:00 Can't Load File Which is ini/activity_main.lua
                """);

            var result = await RunDataToolAsync("client-startup-audit", logPath, "--all");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup warning groups: 2", result.StandardOutput);
            Assert.Contains("Group 1/2: lines 1-1 time=08:00:00 warnings=1", result.StandardOutput);
            Assert.Contains("data2/playskin/default/IMAGE_BACKGROUNDPIC.png count=1", result.StandardOutput);
            Assert.Contains("Group 2/2: lines 2-2 time=08:06:00 warnings=1", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua count=1", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CurrentClientTailTimestampWarningsReportOnlyLatestLaunchByDefault()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-tail-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_4.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(
                logPath,
                """
                =========Lua error: [string "CLuaVM::requirebuffer()"]:86: too many C levels (limit is 200) in function at line 68 near 'ToStringEx' -- Sat Jul  4 08:10:28 2026
                =========Lua error: cannot open ini/activity_main.lua: No such file or directory -- Sat Jul  4 08:10:30 2026
                =========Lua error: cannot open ini/singleemotion.lua: No such file or directory -- Sat Jul  4 08:10:30 2026
                ERROR: file ini/Appkey.dat not found -- Sat Jul  4 08:10:33 2026
                error data: NpcType2089 of ini/Npc.ini -- Sat Jul  4 08:10:38 2026
                error data in [5900] of ini/NpcX.ini -- Sat Jul  4 08:10:38 2026
                =========Lua error: [string "CLuaVM::requirebuffer()"]:86: too many C levels (limit is 200) in function at line 68 near 'ToStringEx' -- Sat Jul  4 08:13:46 2026
                =========Lua error: cannot open ini/activity_main.lua: No such file or directory -- Sat Jul  4 08:13:48 2026
                =========Lua error: cannot open ini/singleemotion.lua: No such file or directory -- Sat Jul  4 08:13:48 2026
                ERROR: file ini/Appkey.dat not found -- Sat Jul  4 08:13:51 2026
                """);

            var result = await RunDataToolAsync("client-startup-audit", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup warning groups: 2", result.StandardOutput);
            Assert.Contains("Latest group: 2/2 lines 7-10 time=08:13:46-08:13:51 warnings=4", result.StandardOutput);
            Assert.Contains("Lua errors: 1", result.StandardOutput);
            Assert.Contains("near 'ToStringEx' count=1 firstLine=7 lastLine=7", result.StandardOutput);
            Assert.Contains("Missing files: 3", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua count=1 firstLine=8 lastLine=8", result.StandardOutput);
            Assert.Contains("ini/singleemotion.lua count=1 firstLine=9 lastLine=9", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat count=1 firstLine=10 lastLine=10", result.StandardOutput);
            Assert.Contains("Error data rows: 0", result.StandardOutput);
            Assert.DoesNotContain("ini/Npc.ini count=1", result.StandardOutput);
            Assert.DoesNotContain("ini/NpcX.ini count=1", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LatestGroupReportsNamedPipeAndFontRendererFailures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-font-renderer-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_6.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(
                logPath,
                """
                Failed to open the appointed named pipe -- Mon Jul 06 09:54:40 2026
                Can not find font: [client\Font\GUI\arial.ttf 14] -- Mon Jul 06 09:54:40 2026
                ��ASSERT(!s_FontSet.empty()) in 226 line of .\TextRender.cpp�� -- Mon Jul 06 09:54:40 2026
                Can not find font: [client\Font\GUI\arial.ttf 16] -- Mon Jul 06 09:54:40 2026
                ��ASSERT(s_FontSet.size() > 0) in 220 line of .\TextRender.cpp�� -- Mon Jul 06 09:54:40 2026
                """);

            var result = await RunDataToolAsync("client-startup-audit", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Recognized warning lines: 5", result.StandardOutput);
            Assert.Contains("Latest group: 1/1 lines 1-5 time=09:54:40 warnings=5", result.StandardOutput);
            Assert.Contains("Font renderer failures: 4", result.StandardOutput);
            Assert.Contains("client/Font/GUI/arial.ttf 14 count=1", result.StandardOutput);
            Assert.Contains("client/Font/GUI/arial.ttf 16 count=1", result.StandardOutput);
            Assert.Contains("TextRender.cpp#s_FontSet count=2", result.StandardOutput);
            Assert.Contains("Named pipe failures: 1", result.StandardOutput);
            Assert.Contains("named-pipe/open count=1", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StagedClientProbePatternReportsInvalidIdentAndReferenceOnlyLuaFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-probes-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_4.log");
        var clientRoot = Path.Combine(tempRoot, "client-staged-7896");
        var baselineRoot = Path.Combine(tempRoot, "client-baseline");
        var donorRoot = Path.Combine(tempRoot, "client-donor-7896");
        var source7770Root = Path.Combine(tempRoot, "source-7770-client");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            Directory.CreateDirectory(clientRoot);
            Directory.CreateDirectory(baselineRoot);
            Directory.CreateDirectory(donorRoot);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "3dtexture.ini"));
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ani", "Control.ani"),
                """
                [existing_client_index]
                Frame0=c3/texture/360364.dds
                """);
            await WriteFixtureFileAsync(
                Path.Combine(baselineRoot, "ani", "Control.ani"),
                """
                [legacy_index]
                """);
            await WriteFixtureFileAsync(
                Path.Combine(donorRoot, "ani", "Control.ani"),
                """
                [hero_pro_7]
                Frame0=c3/texture/360364.dds
                [PirateJX_ui5]
                Frame1=c3/texture/360364.dds
                """);
            await WriteFixtureFileAsync(Path.Combine(donorRoot, "c3", "texture", "360364.dds"));
            await WriteFixtureFileAsync(Path.Combine(source7770Root, "ini", "activity_main.lua"));
            await WriteFixtureFileAsync(Path.Combine(source7770Root, "ini", "singleemotion.lua"));
            await WriteFixtureFileAsync(Path.Combine(source7770Root, "ini", "luacfg", "activity_main.dat"));
            await WriteFixtureFileAsync(Path.Combine(source7770Root, "ini", "luacfg", "singleemotion.dat"));
            await WriteFixtureFileAsync(Path.Combine(source7770Root, "ini", "Appkey.dat"));
            await File.WriteAllTextAsync(
                logPath,
                """
                texture [61409] not found! -- Sat Jul 04 13:22:08 2026
                Invalid Ident: ini/3dtexture.dbc -- Sat Jul 04 13:34:33 2026
                =========Lua error: [string "CLuaVM::requirebuffer()"]:5534: attempt to index a nil value (field '?') -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	<call stack> -- Sat Jul  4 13:34:37 2026
                =========Lua error: ->	__newindex() : line -1 [=[C] : line -1] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	unknown : line 5534 [CLuaVM::requirebuffer() : line 0] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	c_require() : line -1 [=[C] : line -1] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	unknown : line 6 [@ini/basedef.lua : line 4] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	unknown : line 7 [CLuaVM::requirebuffer() : line 0] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	n_require() : line 6 [@ini/basedef.lua : line 4] -- Sat Jul  4 13:34:37 2026
                =========Lua error: 	unknown : line 59 [@ini/basedef.lua : line 58] -- Sat Jul  4 13:34:37 2026
                =========Lua error: (null) -- Sat Jul  4 13:34:37 2026
                =========Lua error: cannot open ini/activity_main.lua: No such file or directory -- Sat Jul  4 13:34:40 2026
                =========Lua error: cannot open ini/singleemotion.lua: No such file or directory -- Sat Jul  4 13:34:40 2026
                WARNING: Read Error in ItemType.dat, itemtypeID=4039109 -- Sat Jul  4 13:34:41 2026
                ERROR: file ini/Appkey.dat not found -- Sat Jul  4 13:34:45 2026
                Error: CreateNewAni(ani/Control.ani, hero_pro_7) failed. -- Sat Jul  4 13:34:46 2026
                Ani Index [PirateJX_ui5] Not Found In [ani/Control.ani] -- Sat Jul  4 13:34:47 2026
                texture [360364] not found! -- Sat Jul 04 13:34:50 2026
                """);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot,
                "--reference-root",
                $"baseline={baselineRoot}",
                $"--reference-root=donor7896={donorRoot}",
                $"--reference-root=source7770={source7770Root}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup warning groups: 2", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat status=missing matches=none", result.StandardOutput);
            Assert.Contains("ini/3dtexture.dbc status=invalid-ident matches=ini/3dtexture.ini", result.StandardOutput);
            Assert.DoesNotContain("ini/3dtexture.dbc status=fallback-present", result.StandardOutput);
            Assert.DoesNotContain("ini/Appkey.dat status=invalid-ident", result.StandardOutput);
            Assert.Contains("Latest group: 2/2 lines 2-19 time=13:34:33-13:34:50 warnings=17", result.StandardOutput);
            Assert.Contains("Lua errors: 10", result.StandardOutput);
            Assert.Contains("[string \"CLuaVM::requirebuffer()\"]:5534: attempt to index a nil value", result.StandardOutput);
            Assert.Contains("Invalid ident files: 1", result.StandardOutput);
            Assert.Contains("ini/3dtexture.dbc count=1 firstLine=2 lastLine=2", result.StandardOutput);
            Assert.Contains("Invalid ident provenance:", result.StandardOutput);
            Assert.Contains("ini/3dtexture.dbc clientRootPresent=false references=none", result.StandardOutput);
            Assert.Contains("Missing textures: 1", result.StandardOutput);
            Assert.Contains("texture:360364 count=1 firstLine=19 lastLine=19", result.StandardOutput);
            Assert.Contains("Texture provenance:", result.StandardOutput);
            Assert.Contains("texture:360364 clientRootPresent=false references=donor7896", result.StandardOutput);
            Assert.Contains("matches=donor7896:c3/texture/360364.dds", result.StandardOutput);
            Assert.Contains("aniRefs=clientRoot:ani/Control.ani,donor7896:ani/Control.ani", result.StandardOutput);
            Assert.Contains("ANI warnings: 2", result.StandardOutput);
            Assert.Contains("ani/Control.ani#hero_pro_7 count=1 firstLine=17 lastLine=17", result.StandardOutput);
            Assert.Contains("ani/Control.ani#PirateJX_ui5 count=1 firstLine=18 lastLine=18", result.StandardOutput);
            Assert.Contains("ANI provenance:", result.StandardOutput);
            Assert.Contains("ani/Control.ani#hero_pro_7 clientRoot=file-only references=baseline:file-only,donor7896:file+index", result.StandardOutput);
            Assert.Contains("ani/Control.ani#PirateJX_ui5 clientRoot=file-only references=baseline:file-only,donor7896:file+index", result.StandardOutput);
            Assert.Contains("Missing files: 3", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua clientRootPresent=false references=source7770", result.StandardOutput);
            Assert.Contains("ini/singleemotion.lua clientRootPresent=false references=source7770", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat clientRootPresent=false references=source7770", result.StandardOutput);
            Assert.DoesNotContain("ini/activity_main.lua clientRootPresent=false references=baseline", result.StandardOutput);
            Assert.DoesNotContain("ini/activity_main.lua clientRootPresent=false references=donor7896", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LuaRequireBufferEvidenceReportsLineAndClientRootCandidates()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-lua-evidence-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_6.log");
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "basedef.lua"), CreateLuaLineFixture(58));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "large_module.lua"), CreateLuaLineFixture(5534));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "lua", "bigger_module.lua"), CreateLuaLineFixture(5535));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "Env_DX9", "ini", "large_module.lua"), CreateLuaLineFixture(5534));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, ".codex-backup-lua", "large_module.lua"), CreateLuaLineFixture(5534));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini.oldcompat-exactold-20260707", "large_module.lua"), CreateLuaLineFixture(5534));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, ".artifacts", "large_module.lua"), CreateLuaLineFixture(5534));
            await File.WriteAllTextAsync(
                logPath,
                """
                =========Lua error: [string "CLuaVM::requirebuffer()"]:5534: attempt to index a nil value (field '?') -- Mon Jul  6 09:54:27 2026
                =========Lua error: 	unknown : line 5534 [CLuaVM::requirebuffer() : line 0] -- Mon Jul  6 09:54:27 2026
                =========Lua error: 	unknown : line 6 [@ini/basedef.lua : line 4] -- Mon Jul  6 09:54:27 2026
                =========Lua error: 	unknown : line 59 [@ini/basedef.lua : line 58] -- Mon Jul  6 09:54:27 2026
                """);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Lua failure evidence:", result.StandardOutput);
            Assert.Contains("requirebuffer line=5534 occurrences=2 firstLogLine=1 lastLogLine=2", result.StandardOutput);
            Assert.Contains("sourceFrames=ini/basedef.lua:4,ini/basedef.lua:58", result.StandardOutput);
            Assert.Contains("clientRootLuaLineCandidates=ini/large_module.lua(lines>=5534),lua/bigger_module.lua(lines>=5534)", result.StandardOutput);
            Assert.DoesNotContain("ini/basedef.lua(lines>=5534)", result.StandardOutput);
            Assert.DoesNotContain("Env_DX9/ini/large_module.lua", result.StandardOutput);
            Assert.DoesNotContain(".codex-backup-lua/large_module.lua", result.StandardOutput);
            Assert.DoesNotContain("ini.oldcompat-exactold-20260707/large_module.lua", result.StandardOutput);
            Assert.DoesNotContain(".artifacts/large_module.lua", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LuaRequireBufferEvidenceHintsGeneratedBufferWhenNoPlainLuaCandidateExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-lua-generated-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_7.log");
        var clientRoot = Path.Combine(tempRoot, "client");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            Directory.CreateDirectory(clientRoot);
            await File.WriteAllTextAsync(
                logPath,
                """
                =========Lua error: [string "CLuaVM::requirebuffer()"]:86: too many C levels (limit is 200) in function at line 68 near 'ToStringEx' -- Tue Jul  7 09:54:27 2026
                """);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Lua failure evidence:", result.StandardOutput);
            Assert.Contains(
                "requirebuffer line=86 occurrences=1 firstLogLine=1 lastLogLine=1 sourceFrames=none clientRootLuaLineCandidates=none hint=near 'ToStringEx'; likely generated/decompressed Lua buffer rather than a plain root Lua file",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LuaRequireBufferEvidenceReportsLineWithoutClientRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-lua-line-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_6.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(
                logPath,
                """
                =========Lua error: [string "CLuaVM::requirebuffer()"]:5534: attempt to index a nil value (field '?') -- Mon Jul  6 09:54:27 2026
                """);

            var result = await RunDataToolAsync("client-startup-audit", logPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Lua failure evidence:", result.StandardOutput);
            Assert.Contains("requirebuffer line=5534 occurrences=1 firstLogLine=1 lastLogLine=1", result.StandardOutput);
            Assert.Contains("sourceFrames=none clientRootLuaLineCandidates=not-checked", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClientRootReportsStartupCriticalResourcePresence()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-critical-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_5.log");
        var clientRoot = Path.Combine(tempRoot, "client-staged-7896");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(logPath, "clean startup tail with no recognized warnings");
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "activity_main.lua"));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "singleemotion.lua"));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "3dtexture.ini"));
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "ini", "FontSetting.ini"));
            var controlAniContents = string.Concat(Enumerable.Repeat("[noise]\r\n", 10_020)) +
                """
                [Complex_clpic1]
                Frame1=data/interface/complex.dds
                """;
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ani", "Control.ani"),
                controlAniContents);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup warning groups: 0", result.StandardOutput);
            Assert.Contains("Startup-critical resources:", result.StandardOutput);
            Assert.Contains("ini/activity_main.lua status=present matches=ini/activity_main.lua", result.StandardOutput);
            Assert.Contains("ini/CommonTip.lua status=missing matches=none", result.StandardOutput);
            Assert.Contains("ini/singleemotion.lua status=present matches=ini/singleemotion.lua", result.StandardOutput);
            Assert.Contains("ini/Appkey.dat status=missing matches=none", result.StandardOutput);
            Assert.Contains("ini/3dtexture.dbc status=fallback-present matches=ini/3dtexture.ini", result.StandardOutput);
            Assert.Contains("ini/Font.ini status=missing matches=none", result.StandardOutput);
            Assert.Contains("ini/Font.ini#Font/GB13000.ttq 14 status=missing matches=none", result.StandardOutput);
            Assert.Contains("Font/GB13000.ttq status=missing matches=none", result.StandardOutput);
            Assert.Contains("ini/FontSetting.ini status=present matches=ini/FontSetting.ini", result.StandardOutput);
            Assert.Contains("ani/Control.ani#Complex_clpic1 status=file+index matches=ani/Control.ani#Complex_clpic1", result.StandardOutput);
            Assert.Contains("No startup warnings recognized.", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClientRootReportsCachedDx9GuiFontStartupGap()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-font-gap-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_6.log");
        var clientRoot = Path.Combine(tempRoot, "client-cached");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(logPath, "clean startup tail with no recognized warnings");
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Font.ini"),
                """
                Font/GUI/tahoma.ttf 12
                Font/GUI/tahoma.ttf 14
                # Font/GB13000.ttq 14
                Font/GB13000.ttq 140
                """);

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup-critical resources:", result.StandardOutput);
            Assert.Contains("ini/Font.ini status=present matches=ini/Font.ini", result.StandardOutput);
            Assert.Contains("ini/Font.ini#Font/GB13000.ttq 14 status=file-only matches=ini/Font.ini", result.StandardOutput);
            Assert.Contains("Font/GB13000.ttq status=missing matches=none", result.StandardOutput);
            Assert.Contains("No startup warnings recognized.", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ClientRootReportsCachedDx9GuiFontStartupResourcesWhenPresent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-client-startup-audit-font-present-" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempRoot, "debug", "2026_7_6.log");
        var clientRoot = Path.Combine(tempRoot, "client-cached");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(logPath, "clean startup tail with no recognized warnings");
            await WriteFixtureFileAsync(
                Path.Combine(clientRoot, "ini", "Font.ini"),
                """
                Font/GB13000.ttq 14
                """);
            await WriteFixtureFileAsync(Path.Combine(clientRoot, "Font", "GB13000.ttq"), "fake font fixture");

            var result = await RunDataToolAsync(
                "client-startup-audit",
                logPath,
                "--client-root",
                clientRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Startup-critical resources:", result.StandardOutput);
            Assert.Contains("ini/Font.ini status=present matches=ini/Font.ini", result.StandardOutput);
            Assert.Contains("ini/Font.ini#Font/GB13000.ttq 14 status=file+entry matches=ini/Font.ini#Font/GB13000.ttq 14", result.StandardOutput);
            Assert.Contains("Font/GB13000.ttq status=present matches=Font/GB13000.ttq", result.StandardOutput);
            Assert.Contains("No startup warnings recognized.", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MissingLogPathReturnsUsageAndExitOne()
    {
        var result = await RunDataToolAsync("client-startup-audit");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Missing client debug log path.", result.StandardError);
        Assert.Contains(
            "Usage: WolfConquer.DataTool client-startup-audit <logPath> [--all] [--top N] [--gap-minutes N] [--client-root path] [--reference-root label=path]",
            result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesClientStartupAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-startup-audit <logPath> [--all] [--top N] [--gap-minutes N] [--client-root path] [--reference-root label=path]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static async Task WriteFixtureFileAsync(string path, string contents = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
    }

    private static string CreateLuaLineFixture(int lineCount)
    {
        return string.Concat(Enumerable.Range(1, lineCount).Select(line => $"-- line {line}{Environment.NewLine}"));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
