using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class SettingsStoreTests
{
    /// <summary>
    /// Hand-rolled fake over the secret-protection seam: a trivially reversible
    /// byte-flip. Reversible enough to prove the round-trip, but unmistakably
    /// not plaintext, so tests can also assert the key is never written as-is —
    /// without depending on real DPAPI (covered by its own test).
    /// </summary>
    private sealed class FlipProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => Flip(plaintext);
        public byte[] Unprotect(byte[] ciphertext) => Flip(ciphertext);

        private static byte[] Flip(byte[] bytes)
        {
            var copy = new byte[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
                copy[i] = (byte)~bytes[i];
            return copy;
        }
    }

    private static string TempRoot() => Directory.CreateTempSubdirectory("blurt-test-").FullName;

    [Fact]
    public void Load_returns_the_built_in_defaults_when_no_config_file_exists()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            var config = store.Load();

            Assert.Equal(BlurtConfig.Default, config);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Saved_config_round_trips_back_to_an_equal_value()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());
            var config = BlurtConfig.Default with
            {
                Transcription = TranscriptionMode.Online,
                WhisperModel = new WhisperModel("base", "q5_1"),
                RefinementProvider = RefinementProvider.LocalOpenAiCompatible,
                RefinementBaseUrl = "http://localhost:11434/v1",
                RefinementModel = "llama3.1",
                // Per-provider endpoint memory (issue 24) must round-trip too.
                RefinementEndpoints = new Dictionary<RefinementProvider, RefinementEndpoint>
                {
                    [RefinementProvider.OpenAi] = new()
                    {
                        BaseUrl = "https://eu.proxy.example/v1",
                        Model = "gpt-4.1",
                    },
                    [RefinementProvider.LocalOpenAiCompatible] = new()
                    {
                        BaseUrl = "http://localhost:11434/v1",
                        Model = "llama3.1",
                    },
                },
                HotkeyBindings = new Dictionary<TriggerKind, string>
                {
                    [TriggerKind.Fix] = "Ctrl+F1",
                    [TriggerKind.English] = "Ctrl+F2",
                    [TriggerKind.FlexSlot] = "Ctrl+F3",
                },
                FlexSlotOrder = [FlexSlotMode.Custom, FlexSlotMode.Pur, FlexSlotMode.Bullets],
                // Per-mode editable prompts (issue 35; Email added in issue 36) must round-trip too.
                FixPrompt = "Just fix the punctuation.",
                EnglishPrompt = "Translate to British English.",
                BulletsPrompt = "Terse bullets, no sub-points.",
                EmailPrompt = "Keep the email short and formal.",
                CustomPrompt = "Translate to formal German.",
                // The prompt-reset backup slot (issue 37) must round-trip too.
                PromptBackup = new PromptSnapshot
                {
                    FixPrompt = "old fix",
                    EnglishPrompt = "old english",
                    BulletsPrompt = "old bullets",
                    EmailPrompt = "old email",
                    CustomPrompt = "old custom",
                },
                OverlayAnchor = OverlayAnchor.BottomCenter,
                SoundEnabled = true,
                InputDeviceMode = InputDeviceMode.Specific,
                InputDeviceName = "Headset (Bluetooth)",
            };

            store.Save(config);
            var loaded = store.Load();

            Assert.Equal(config, loaded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Loading_a_legacy_config_with_the_removed_base_model_does_not_crash()
    {
        // Migration safety (issue 18): the "base" model was removed, but an existing
        // on-disk config.json may still carry WhisperModel.Size == "base". Loading it
        // must not throw — BlurtConfig just carries whatever was persisted, and the
        // Settings UI falls back to small when the stored size matches no option.
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());
            Directory.CreateDirectory(Path.Combine(root, "Blurt"));
            File.WriteAllText(store.ConfigPath, """
                {
                  "Transcription": "Local",
                  "WhisperModel": { "Size": "base", "Quantization": "q5_1" },
                  "OnboardingCompleted": true
                }
                """);

            var config = store.Load();

            Assert.Equal("base", config.WhisperModel.Size);
            Assert.True(config.OnboardingCompleted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GpuPreference_round_trips_and_an_absent_field_resolves_to_auto()
    {
        // Issue 42 / ADR-0001: the new GPU-acceleration preference persists like any
        // other setting, and a config.json written before it existed (no key for it)
        // must resolve to Auto — so existing installs get GPU-on after upgrade.
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            store.Save(BlurtConfig.Default with { GpuPreference = GpuPreference.Off });
            Assert.Equal(GpuPreference.Off, store.Load().GpuPreference);

            // An older config with no GpuPreference key loads as Auto.
            Directory.CreateDirectory(Path.Combine(root, "Blurt"));
            File.WriteAllText(store.ConfigPath, """{ "Transcription": "Local" }""");
            Assert.Equal(GpuPreference.Auto, store.Load().GpuPreference);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Loading_a_config_without_the_per_mode_prompt_keys_uses_the_shipped_defaults()
    {
        // Backward compatibility (issue 35, criterion 3): a config.json written
        // before the editable per-mode prompts existed has no Fix/English/Bullets
        // prompt keys. Loading it must leave each at its shipped default so the
        // install behaves exactly as before — proven by resolving through ModePrompts.
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());
            Directory.CreateDirectory(Path.Combine(root, "Blurt"));
            File.WriteAllText(store.ConfigPath, """
                {
                  "Transcription": "Local",
                  "RefinementModel": "gpt-4o-mini",
                  "CustomPrompt": "Speak like a pirate.",
                  "OnboardingCompleted": true
                }
                """);

            var config = store.Load();

            Assert.Equal(RefinementPrompts.Fix, config.FixPrompt);
            Assert.Equal(RefinementPrompts.English, config.EnglishPrompt);
            Assert.Equal(RefinementPrompts.Bullets, config.BulletsPrompt);
            // Email (issue 36) is newer still — an older config has no key for it
            // and must deserialise to the shipped default too.
            Assert.Equal(RefinementPrompts.Email, config.EmailPrompt);
            // The one key that was present is still honoured.
            Assert.Equal("Speak like a pirate.", config.CustomPrompt);
            // The prompt-reset backup slot (issue 37) is absent in an older config —
            // it deserialises to null (no backup yet), not a crash.
            Assert.Null(config.PromptBackup);
            // And resolution falls back to the shipped wording for the absent keys.
            Assert.Equal(RefinementPrompts.Fix, ModePrompts.For(RefinedMode.Fix, config));
            Assert.Equal(RefinementPrompts.English, ModePrompts.For(RefinedMode.English, config));
            Assert.Equal(RefinementPrompts.Email, ModePrompts.For(RefinedMode.Email, config));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Onboarding_completed_flag_round_trips_both_ways()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            // Default config (onboarding not yet done) round-trips as not-completed.
            store.Save(BlurtConfig.Default);
            Assert.False(store.Load().OnboardingCompleted);

            // And once the wizard marks it done, that survives the JSON round-trip —
            // proving the flag is in the overridden Equals/GetHashCode (otherwise the
            // round-trip equality below would fail).
            var completed = BlurtConfig.Default with { OnboardingCompleted = true };
            store.Save(completed);
            var loaded = store.Load();
            Assert.True(loaded.OnboardingCompleted);
            Assert.Equal(completed, loaded);
            Assert.NotEqual(BlurtConfig.Default, loaded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Config_is_written_as_readable_json_at_the_expected_path()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            store.Save(BlurtConfig.Default);

            Assert.Equal(Path.Combine(root, "Blurt", "config.json"), store.ConfigPath);
            Assert.True(File.Exists(store.ConfigPath));
            var text = File.ReadAllText(store.ConfigPath);
            // Readable JSON: contains recognisable field values in clear text.
            Assert.Contains("gpt-4o-mini", text);
            Assert.Contains("Local", text);   // enum serialised by name, not number
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Api_key_round_trips_through_save_then_load()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            store.SaveApiKey("sk-secret-123");

            Assert.Equal("sk-secret-123", store.LoadApiKey());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Loading_the_api_key_before_any_was_saved_returns_null()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());

            Assert.Null(store.LoadApiKey());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Stored_api_key_file_does_not_contain_the_plaintext_key()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());
            const string key = "sk-super-secret-key";

            store.SaveApiKey(key);

            var raw = File.ReadAllBytes(store.ApiKeyPath);
            Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(key), raw);
            // And the readable form must not leak the key either.
            Assert.DoesNotContain(key, System.Text.Encoding.UTF8.GetString(raw));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Config_and_secret_live_in_separate_files_under_the_blurt_folder()
    {
        var root = TempRoot();
        try
        {
            var store = new SettingsStore(root, new FlipProtector());
            var blurtDir = Path.Combine(root, "Blurt");

            Assert.Equal(Path.Combine(blurtDir, "config.json"), store.ConfigPath);
            Assert.Equal(Path.Combine(blurtDir, "apikey.dat"), store.ApiKeyPath);
            Assert.NotEqual(store.ConfigPath, store.ApiKeyPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
