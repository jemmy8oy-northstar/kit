using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Kestrel serves a read while a write is in flight, which <c>ui.js</c> never could: its
/// <c>writeFileSync</c> finished before any other request ran. The edit lock serialises writes
/// against each other, but no GET takes it, so a write that truncates the file and then fills
/// it lets a page read the corpus in between — and render it as empty or half a file.
/// </summary>
public class TornReadTests
{
    [Fact]
    public async Task A_read_during_a_write_sees_the_whole_old_file_or_the_whole_new_one()
    {
        var dir = Directory.CreateTempSubdirectory("kit-torn-").FullName;
        try
        {
            var corpora = new CorpusDirectory(dir, dir);

            // Big enough that one write is many syscalls, so a reader can land inside it.
            var a = string.Concat(Enumerable.Repeat("behaviour BEH-1 \"a\"\n  when opens page:Home\n", 40_000));
            var b = string.Concat(Enumerable.Repeat("behaviour BEH-2 \"b\"\n  when opens page:Away\n", 40_000));
            corpora.WriteText("demo", a);
            corpora.WriteBindingsText("demo", "{\"k\":\"" + new string('x', 1_000_000) + "\"}");

            var stop = DateTime.UtcNow.AddSeconds(2);
            var writer = Task.Run(() =>
            {
                for (var i = 0; DateTime.UtcNow < stop; i++)
                {
                    corpora.WriteText("demo", i % 2 == 0 ? b : a);
                    corpora.WriteBindingsText("demo", "{\"k\":\"" + new string(i % 2 == 0 ? 'y' : 'x', 1_000_000) + "\"}");
                }
            });

            var torn = 0;
            var tornBindings = 0;
            var reader = Task.Run(() =>
            {
                while (DateTime.UtcNow < stop)
                {
                    // Measured before the fix: ReadAllBytes throwing EndOfStreamException, because
                    // the file shrank between its length check and its read. That is a 500.
                    try
                    {
                        var text = corpora.ReadText("demo");
                        if (text != a && text != b)
                        {
                            torn++;
                        }
                    }
                    catch (IOException)
                    {
                        torn++;
                    }

                    try
                    {
                        corpora.Bindings("demo");
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        tornBindings++;
                    }
                }
            });

            await Task.WhenAll(writer, reader);
            Assert.Equal(0, torn);
            Assert.Equal(0, tornBindings);

            // No temporary file is left beside the corpus for a listing or a `git add -A` to find.
            Assert.Equal(new[] { "demo.beh", "demo.bindings.json" }, Directory.GetFiles(dir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
