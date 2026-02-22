using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace WidgetApi.Services;

// Exact copy from BitopiPpc.DAL — DO NOT MODIFY
// Must match PPC-Web encryption to authenticate against tblUser
public class EncryptionService
{
    public string EncryptWord(string EncryptIt)
    {
        return EncryptWord(EncryptIt, "");
    }

    private string EncryptWord(string EncryptIt, string Weight)
    {
        string str3 = "";
        if (Strings.Len(Strings.Trim(EncryptIt)) == 0) return "";

        long num2 = ReturnWeight(Weight);
        VBMath.Rnd(-1f);
        if (Strings.Len(Strings.Trim(Weight)) == 0)
            VBMath.Randomize(5.0);
        else
            VBMath.Randomize((double)num2);

        for (long i = 1L; i <= Strings.Len(EncryptIt); i += 1L)
        {
            long num = Strings.Asc(Strings.Mid(EncryptIt, (int)i, 1)) + ((int)Math.Round((double)((100f * VBMath.Rnd()) + 1f)));
            if (num > 0xffL) num -= 0xffL;
            if (num < 1L) num += 0xffL;
            str3 = str3 + StringType.FromChar(Strings.Chr((int)num));
        }
        return str3;
    }

    private long ReturnWeight(string Weight)
    {
        object? obj2 = null;
        for (long i = 1L; i <= Strings.Len(Weight); i += 1L)
            obj2 = ObjectType.AddObj(obj2, Strings.Asc(Strings.Mid(Weight, (int)i, 1)));
        return LongType.FromObject(obj2);
    }
}
