using eHub.Authentication.Models;
using eHub.Config;
using Microsoft.Extensions.Options;

namespace eHub.Authentication.Service;

public interface IUserService
{
    Task<User?> Authenticate(string username, string password);
    Task<IEnumerable<User>> GetAll();
}

public class UserService(IOptionsSnapshot<AuthenticationConfig> authConfig) : IUserService
{
    private readonly AuthenticationConfig _authConfig = authConfig.Value;

    /// <summary></summary>
    /// <returns>
    /// on auth fail: <see langword="null" /> is returned because user is not found<br/>
    /// on auth success: <see cref="User"/> object is returned
    /// </returns>
    public async Task<User?> Authenticate(string username, string password)
    {
        return !_authConfig.Enabled
            ? new User { Username = username, Password = password }
            : (await GetAll()).SingleOrDefault(x => x.Username == username && x.Password == password);
    }

    public Task<IEnumerable<User>> GetAll()
    {
		if (_authConfig.Basic is null)
		{
			return Task.FromResult<IEnumerable<User>>([]);
		}

		IEnumerable<User> userList = [];

        // Inject dummy users from appsettings.json
        // NOTE: Remove in production
        userList = userList.Concat(_authConfig.Basic!.DummyUsers.Select(dummy => new User()
        {
            Username = dummy.Username,
            Password = dummy.Password,
        }));

        // NOTE: use async when fetching users from a real db
        return Task.FromResult(userList);
    }
}
