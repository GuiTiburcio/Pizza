using System.Security.Cryptography;
using System.Text;

namespace Pizza.Core.Output;

/// <summary>
/// Diário dos temporários que o Pizza criou, protegido por DPAPI. Depois de uma queda, a limpeza usa
/// só o que está aqui: nunca apaga por padrão de nome, então não toca em arquivo de outra pessoa (SEC-37).
/// </summary>
public sealed class TempJournal(string path)
{
    private readonly List<Entry> _entries = Load(path);

    public sealed record Entry(string Root, ulong VolumeSerial, byte[] FileId);

    public void Add(Entry entry)
    {
        _entries.Add(entry);
        Save();
    }

    public void Remove(Entry entry)
    {
        _entries.Remove(entry);
        Save();
    }

    /// <summary>Apaga os temporários que sobraram de uma execução anterior e esvazia o diário.</summary>
    public void CleanOrphans()
    {
        foreach (var entry in _entries)
        {
            FileNative.DeleteOrphan(entry);
        }

        _entries.Clear();
        Save();
    }

    private static List<Entry> Load(string path)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(FileNative.Unprotect(File.ReadAllBytes(path))), Encoding.UTF8);
            var entries = new List<Entry>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                entries.Add(new Entry(reader.ReadString(), reader.ReadUInt64(), reader.ReadBytes(16)));
            }

            return entries;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception)
        {
            // Sem diário legível não há o que limpar, e nada é apagado por suposição.
            return [];
        }
    }

    private void Save()
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(_entries.Count);
            foreach (var entry in _entries)
            {
                writer.Write(entry.Root);
                writer.Write(entry.VolumeSerial);
                writer.Write(entry.FileId);
            }
        }

        var temporary = path + ".novo";
        File.WriteAllBytes(temporary, FileNative.Protect(buffer.ToArray()));
        File.Move(temporary, path, overwrite: true);
    }
}
