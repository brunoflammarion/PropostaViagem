namespace SistemaUsuarios.Services
{
    public class PassengerAgeService : IPassengerAgeService
    {
        public int GetAgeOnDate(DateTime birthDate, DateTime referenceDate)
        {
            var age = referenceDate.Year - birthDate.Year;
            if (birthDate.Month > referenceDate.Month ||
                (birthDate.Month == referenceDate.Month && birthDate.Day > referenceDate.Day))
                age--;
            return age;
        }

        public PassengerAgeCategory GetCategory(DateTime birthDate, DateTime referenceDate)
        {
            var age = GetAgeOnDate(birthDate, referenceDate);
            return age switch
            {
                < 2  => PassengerAgeCategory.Bebe,
                < 12 => PassengerAgeCategory.Crianca,
                < 18 => PassengerAgeCategory.Adolescente,
                _    => PassengerAgeCategory.Adulto
            };
        }

        public string GetCategoryLabel(PassengerAgeCategory category) => category switch
        {
            PassengerAgeCategory.Bebe        => "Bebê",
            PassengerAgeCategory.Crianca     => "Criança",
            PassengerAgeCategory.Adolescente => "Adolescente",
            _                                => "Adulto"
        };

        public string GetCategoryRange(PassengerAgeCategory category) => category switch
        {
            PassengerAgeCategory.Bebe        => "menos de 2 anos",
            PassengerAgeCategory.Crianca     => "2 a 11 anos",
            PassengerAgeCategory.Adolescente => "12 a 17 anos",
            _                                => "18 anos ou mais"
        };
    }
}
