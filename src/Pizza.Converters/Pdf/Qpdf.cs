using System.Runtime.InteropServices;

namespace Pizza.Converters.Pdf;

/// <summary>
/// A API em C da QPDF (ADR-033), só o que o Pizza usa. A leitura é sempre da memória: nenhum caminho de
/// arquivo chega à biblioteca (SEC-15).
/// </summary>
internal static partial class Qpdf
{
    private const string Library = "qpdf30";

    public const int Success = 0;
    public const int Errors = 1 << 1;

    public const int ErrorPassword = 4; // qpdf_e_password
    public const int ObjectStreamsGenerate = 2; // qpdf_o_generate

    [LibraryImport(Library, EntryPoint = "qpdf_init")]
    public static partial nint Init();

    [LibraryImport(Library, EntryPoint = "qpdf_cleanup")]
    public static partial void Cleanup(ref nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_silence_errors")]
    public static partial void SilenceErrors(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_set_suppress_warnings")]
    public static partial void SuppressWarnings(nint qpdf, int value);

    [LibraryImport(Library, EntryPoint = "qpdf_get_error")]
    public static partial nint GetError(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_error_code")]
    public static partial int GetErrorCode(nint qpdf, nint error);

    [LibraryImport(Library, EntryPoint = "qpdf_read_memory", StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial int ReadMemory(nint qpdf, string description, byte* buffer, ulong size, byte* password);

    [LibraryImport(Library, EntryPoint = "qpdf_empty_pdf")]
    public static partial int EmptyPdf(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_add_page")]
    public static partial int AddPage(nint qpdf, nint pageOwner, uint page, int first);

    [LibraryImport(Library, EntryPoint = "qpdf_is_encrypted")]
    public static partial int IsEncrypted(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_num_pages")]
    public static partial int PageCount(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_page_n")]
    public static partial uint Page(nint qpdf, nuint index);

    [LibraryImport(Library, EntryPoint = "qpdf_push_inherited_attributes_to_page")]
    public static partial int PushInheritedAttributesToPage(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_root")]
    public static partial uint Root(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_trailer")]
    public static partial uint Trailer(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_object_by_id")]
    public static partial uint ObjectById(nint qpdf, int id, int generation);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_object_id")]
    public static partial int ObjectId(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_generation")]
    public static partial int Generation(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_begin_dict_key_iter")]
    public static partial void BeginKeys(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_dict_more_keys")]
    public static partial int MoreKeys(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_dict_next_key")]
    public static partial nint NextKey(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_is_dictionary")]
    public static partial int IsDictionary(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_is_stream")]
    public static partial int IsStream(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_dict")]
    public static partial uint StreamDictionary(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_is_array")]
    public static partial int IsArray(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_is_integer")]
    public static partial int IsInteger(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_is_name_and_equals", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int IsName(nint qpdf, uint handle, string name);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_int_value")]
    public static partial long IntValue(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_array_n_items")]
    public static partial int ArrayCount(nint qpdf, uint handle);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_array_item")]
    public static partial uint ArrayItem(nint qpdf, uint handle, int index);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_new_array")]
    public static partial uint NewArray(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_append_item")]
    public static partial void Append(nint qpdf, uint array, uint item);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_new_integer")]
    public static partial uint NewInteger(nint qpdf, long value);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_has_key", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int HasKey(nint qpdf, uint handle, string key);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_get_key", StringMarshalling = StringMarshalling.Utf8)]
    public static partial uint Key(nint qpdf, uint handle, string key);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_replace_key", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void ReplaceKey(nint qpdf, uint handle, string key, uint item);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_remove_key", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void RemoveKey(nint qpdf, uint handle, string key);

    [LibraryImport(Library, EntryPoint = "qpdf_oh_release_all")]
    public static partial void ReleaseAll(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_init_write_memory")]
    public static partial int InitWriteMemory(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_set_object_stream_mode")]
    public static partial void SetObjectStreamMode(nint qpdf, int mode);

    [LibraryImport(Library, EntryPoint = "qpdf_set_suppress_original_object_IDs")]
    public static partial void SuppressOriginalObjectIds(nint qpdf, int value);

    [LibraryImport(Library, EntryPoint = "qpdf_set_deterministic_ID")]
    public static partial void DeterministicId(nint qpdf, int value);

    [LibraryImport(Library, EntryPoint = "qpdf_set_preserve_encryption")]
    public static partial void PreserveEncryption(nint qpdf, int value);

    [LibraryImport(Library, EntryPoint = "qpdf_write")]
    public static partial int Write(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_buffer_length")]
    public static partial nuint BufferLength(nint qpdf);

    [LibraryImport(Library, EntryPoint = "qpdf_get_buffer")]
    public static unsafe partial byte* Buffer(nint qpdf);
}
