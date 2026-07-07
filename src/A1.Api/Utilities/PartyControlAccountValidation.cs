namespace A1.Api.Utilities
{
    public static class PartyControlAccountValidation
    {
        public static string? ValidateDualCoaIds(int? coaId, int? coaId2)
        {
            if (!coaId.HasValue || coaId.Value <= 0)
            {
                return "Control Account is required.";
            }

            if (!coaId2.HasValue || coaId2.Value <= 0)
            {
                return "Control Account 2 is required.";
            }

            if (coaId.Value == coaId2.Value)
            {
                return "Control Account 2 must differ from Control Account.";
            }

            return null;
        }
    }
}
