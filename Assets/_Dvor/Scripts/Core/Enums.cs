namespace Dvor
{
    public enum GamePhase : byte { Lobby = 0, Morning = 1, Day = 2, Meeting = 3, Evening = 4, Finished = 5 }

    public enum Side : byte { None = 0, Dvor = 1, Cop = 2 }

    public enum PlayerStatus : byte { Free = 0, Banished = 1, Arrested = 2 }

    public enum RoleId : byte
    {
        None       = 0,
        Gopnik     = 1,
        Alkash     = 2,
        Sportik    = 3,
        Baryga     = 4,
        Babka      = 5,
        Fortochnik = 6,
        Garazhnik  = 7,
        Petardschik= 8,
        Katala     = 9,
        Raspisnoy  = 10,
        Tsvetmetchik = 11,
        Cop        = 100
    }
}
