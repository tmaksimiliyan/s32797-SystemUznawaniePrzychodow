namespace SystemUznawaniaPrzychodow.DTOs;

public record SignUpRequestDto(
    string Login,
    string Password,
    string Role);
