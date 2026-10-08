namespace Dvor
{
    /// <summary>
    /// Все числа баланса. Менять здесь и нигде больше.
    /// После 30 партий половина этих значений будет другой — так и должно быть.
    /// </summary>
    public static class Balance
    {
        // фазы, секунды
        public const float MorningSeconds = 25f;
        public const float DaySeconds     = 240f;
        public const float MeetingSeconds = 100f;
        public const float EveningSeconds = 20f;

        public const int DaysPerMatch = 4;
        public const int MinPlayers   = 2;   // для отладки вдвоём; для реальной игры 6

        // скука
        public const float BoredomPerMinute     = 6f;
        public const float BoredomCheckMult     = 2f;
        public const float BoredomNervous       = 40f;
        public const float BoredomShaky         = 65f;
        public const float BoredomBreak         = 85f;

        // участковый
        public const float ProtocolDistance    = 8f;
        public const float ProtocolHoldSeconds = 4f;
        public const int   ProtocolsForArrest  = 3;
        public const int   ChecksPerDay        = 3;
        public const float CheckSeconds        = 15f;
        public const float CopReachDistance    = 3.5f;

        // дела
        public const int   DeedsPerMorning = 3;
        /// <summary>Как часто персонаж срывается, пока Скука на максимуме, секунд</summary>
        public const float BreakdownEvery  = 9f;
        public const float WitnessRange    = 20f;
        public const float PartnerRange    = 4f;

        // стукачи
        public const int SnitchesPerTen = 2;

        // победа
        public const int ArrestsToWin     = 3;
        public const int WrongVotesToLose = 2;

        // голос
        public const int   VoiceSampleRate = 8000;
        public const float VoiceFullRange  = 6f;
        public const float VoiceMaxRange   = 22f;

        public static int SnitchCountFor(int players)
        {
            int n = players * SnitchesPerTen / 10;
            if (n < 1) n = 1;
            int cap = players / 3; if (cap < 1) cap = 1;
            return n > cap ? cap : n;
        }

        public static float SecondsFor(GamePhase p) => p switch
        {
            GamePhase.Morning => MorningSeconds,
            GamePhase.Day     => DaySeconds,
            GamePhase.Meeting => MeetingSeconds,
            GamePhase.Evening => EveningSeconds,
            _ => 0f
        };
    }
}
