using Microsoft.AspNetCore.Identity;
using Bit.Fabric.Shared.Features.Identity.Dtos;

namespace Bit.Fabric.Tests.Features.Identity;

/// <summary>
/// Users a test creates for itself. Every test shares one database and they run in parallel, so a test that changes a
/// user's data works on one of these instead of the seeded ones in <see cref="TestData"/>.
/// </summary>
public static class TestUsers
{
    public const string Password = "Scenario-Password-1";

    /// <summary>
    /// A new user, signed up and confirmed through the api the way the sign up and confirm pages do it - the journey
    /// through those pages has a test of its own. A new user has only an e-mail, no full name, so the app calls them
    /// by that e-mail.
    /// </summary>
    public static async Task<string> SignUp(AppTestServer server, CancellationToken cancellationToken)
    {
        var email = NewEmail();

        using var api = server.CreateRawHttpClient();
        // The origin the api builds the e-mailed confirmation link for (See AppTestServer.BuildAppHttpClient).
        api.DefaultRequestHeaders.Add("X-Origin", server.WebAppAddress.ToString());

        // The user name the sign up page sends (See SignUpPage.signUpModel).
        await Post(api, "api/v1/Identity/SignUp", new SignUpRequestDto { UserName = Guid.CreateVersion7().ToString(), Email = email, Password = Password }, cancellationToken);

        var confirmationEmail = await server.WaitForCapturedEmail(email, captured => captured.Kind is CapturedEmailKind.EmailToken, cancellationToken);

        await Post(api, "api/v1/Identity/ConfirmEmail", new ConfirmEmailRequestDto { Email = email, Token = confirmationEmail.Token }, cancellationToken);

        return email;
    }

    private static async Task Post<T>(HttpClient api, string url, T request, CancellationToken cancellationToken)
    {
        using var response = await api.PostAsJsonAsync(url, request, cancellationToken);

        if (response.IsSuccessStatusCode is false)
            throw new InvalidOperationException($"{url} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

    /// <summary>Changes the user's full name on the server, the way an edit made on another device would.</summary>
    public static async Task SetFullName(AppTestServer server, string email, string fullName)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"No user has the e-mail {email}.");
        user.FullName = fullName;

        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded is false)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    /// <summary>An e-mail no other test uses.</summary>
    public static string NewEmail() => $"{Guid.CreateVersion7():N}@scenario.bitplatform.dev";
}
