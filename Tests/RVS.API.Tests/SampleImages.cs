namespace RVS.API.Tests;

/// <summary>
/// Small real image fixtures shared across tests. The HEIC sample is a 96 × 64 gradient
/// encoded as a single-image ISO-BMFF <c>heic</c> file — the shape an iPhone produces and
/// the format QuestPDF's decoder cannot read (issue <c>#492</c> item 8 / <c>#508</c>).
/// </summary>
internal static class SampleImages
{
    /// <summary>A valid 96 × 64 HEIC image (~850 bytes).</summary>
    public static byte[] Heic96x64() => Convert.FromBase64String(Heic96x64Base64);

    private const string Heic96x64Base64 =
        "AAAAJGZ0eXBoZWljAAAAAG1pZjFNaVBybWlhZk1pSEJoZWljAAABgG1ldGEAAAAAAAAAIWhkbHIA" +
        "AAAAAAAAAHBpY3QAAAAAAAAAAAAAAAAAAAAAJGRpbmYAAAAcZHJlZgAAAAAAAAABAAAADHVybCAA" +
        "AAABAAAADnBpdG0AAAAAAAEAAAAjaWluZgAAAAAAAQAAABVpbmZlAgAAAAABAABodmMxAAAAAOBp" +
        "cHJwAAAAwGlwY28AAAATY29scm5jbHgAAgACAAaAAAAAeGh2Y0MBAWAAAACwAAAAAAAe8AD8/fj4" +
        "AAAPA6AAAQAXQAEMAf//AWAAAAMAsAAAAwAAAwAeLAmhAAEAIkIBAQFgAAADALAAAAMAAAMAHqAw" +
        "gQWcuSRKSXE3AgIGpAKiAAEAEUQBwGESTATpEREkSRJEkSpAAAAAFGlzcGUAAAAAAAAAYAAAAEAA" +
        "AAAJaXJvdAAAAAAQcGl4aQAAAAADCAgIAAAAGGlwbWEAAAAAAAAAAQABBYGCA4QFAAAAHmlsb2MA" +
        "AAAARAAAAQABAAAAAQAAAbQAAAGeAAAAAW1kYXQAAAAAAAABrgAAAZomAa3ATSkreGb8VCSvnwJY" +
        "rc/7BfI6QNlrSfu16UcA2R71/lyJ4vq3ZHN/R3HU3fj9p/eDrDuR3bPAC9PYfhBzVu8ADUIaEU5" +
        "w/XwH2wG1NLhaQQBkNqhsGAdX4rWgUByvuCiDewVMVQEInbxnapKWqi9aNsPHQUePnMatAI9P95v" +
        "dtMKkkPOcB4lBlqEtVOlqZM/DZ3fAG5SDHXnG+ndsAR00SW53rsebLnBWgJwiTIBaQMEYq3xK4YJ" +
        "vlqvBpEOPxffr1Dn/T/X/jpSYGsESNjqmgrU+5lB+Is2KURZZidhtIeMV006F7mE3r7dtTW4Rmh" +
        "RDuYNFT8ex9tjh274fpcYcEgWBSVbzi4s6TsenpcayrBOBfM/raJYKWiMYqSC9yQlvIlOppVnEa" +
        "IWCvTHgYAt5Ukk44z1U8JqBKCXqwLtwxD/Nj/FwXH4a+tuM0HLL97OXCE6UBwghF57oVd0cT13K" +
        "6uxN8ywBAoSjn+618PQODNyRNmE4OtI1Xz523PPMa1UtA/q0JA/ijDDudy7ywQ0y3tOXliXl4A==";
}
