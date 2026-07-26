namespace PSMOperationsPlatform.Domain.Common;

internal static class EnumGuard
{
    public static TEnum Defined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be a defined enum member.");
        }

        return value;
    }
}
