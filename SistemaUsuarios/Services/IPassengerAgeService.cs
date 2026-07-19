namespace SistemaUsuarios.Services
{
    public enum PassengerAgeCategory
    {
        Bebe = 1,        // < 2 anos
        Crianca = 2,     // 2 a 11 anos
        Adolescente = 3, // 12 a 17 anos
        Adulto = 4       // >= 18 anos
    }

    public interface IPassengerAgeService
    {
        int GetAgeOnDate(DateTime birthDate, DateTime referenceDate);
        PassengerAgeCategory GetCategory(DateTime birthDate, DateTime referenceDate);
        string GetCategoryLabel(PassengerAgeCategory category);
        string GetCategoryRange(PassengerAgeCategory category);
    }
}
