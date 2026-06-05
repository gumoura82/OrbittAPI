using Microsoft.EntityFrameworkCore;
using OrbittAPI.Domain.Entities;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Exceptions;
using OrbittAPI.Infrastructure.Data;

namespace OrbittAPI.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly OrbittDbContext _db;

    public UserRepository(OrbittDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id) =>
        await _db.Users.FindAsync(id);

    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

    public async Task<User> CreateAsync(User user)
    {
        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("unique") == true)
                throw new BusinessException($"E-mail '{user.Email}' já está em uso.");
            throw;
        }
    }

    public async Task<User> UpdateAsync(User user)
    {
        // Nota: a entidade User chama MarkAsUpdated() em seus métodos
        // de domínio (Upgrade, EnableMfa, Deactivate), então não duplicamos aqui.
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByIdAsync(id) ?? throw new NotFoundException("Usuário", id);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }
}

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly OrbittDbContext _db;

    public ApiKeyRepository(OrbittDbContext db) => _db = db;

    public async Task<ApiKey?> GetByValueAsync(string keyValue) =>
        await _db.ApiKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.KeyValue == keyValue);

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId) =>
        await _db.ApiKeys.Where(k => k.UserId == userId).ToListAsync();

    public async Task<ApiKey> CreateAsync(ApiKey apiKey)
    {
        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();
        return apiKey;
    }

    public async Task<ApiKey> UpdateAsync(ApiKey apiKey)
    {
        _db.ApiKeys.Update(apiKey);
        await _db.SaveChangesAsync();
        return apiKey;
    }
}

public class ApiCallRepository : IApiCallRepository
{
    private readonly OrbittDbContext _db;

    public ApiCallRepository(OrbittDbContext db) => _db = db;

    public async Task<ApiCall> RecordAsync(ApiCall call)
    {
        _db.ApiCalls.Add(call);
        await _db.SaveChangesAsync();
        return call;
    }

    public async Task<int> GetMonthlyCountAsync(Guid userId, int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return await _db.ApiCalls
            .CountAsync(c => c.UserId == userId && c.CalledAt >= start && c.CalledAt < end);
    }

    public async Task<IEnumerable<ApiCall>> GetRecentByUserAsync(Guid userId, int limit = 100) =>
        await _db.ApiCalls
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CalledAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IEnumerable<ApiCall>> GetByUserAndPeriodAsync(Guid userId, DateTime from, DateTime to) =>
        await _db.ApiCalls
            .Where(c => c.UserId == userId && c.CalledAt >= from && c.CalledAt <= to)
            .OrderByDescending(c => c.CalledAt)
            .ToListAsync();
}
