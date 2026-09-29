using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>Helpers that create records through the public API, as a client would.</summary>
internal static class TestData
{
    public static async Task<JsonElement> ReadJson(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        return string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<JsonElement> CreateCustomer(this HttpClient client, string name = "Acme Trading", string? email = "info@acme.sa",
        string? phone = null, string type = "Company")
    {
        var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            type,
            name,
            primaryEmail = email,
            primaryPhone = phone,
            preferredLanguage = "ar",
        }, CrmApiFactory.Json);
        return await response.ReadJson(HttpStatusCode.Created);
    }

    public static async Task<Guid> CategoryId(this CrmApiFactory factory, string nameEn) =>
        await factory.WithDb(db => db.Categories.Where(c => c.NameEn == nameEn).Select(c => c.Id).SingleAsync());

    public static async Task<Guid> DepartmentId(this CrmApiFactory factory, string nameEn) =>
        await factory.WithDb(db => db.Departments.Where(d => d.NameEn == nameEn).Select(d => d.Id).SingleAsync());

    public static async Task<Guid> UserId(this CrmApiFactory factory, string email) =>
        await factory.WithDb(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

    public static async Task<JsonElement> CreateTicket(this HttpClient client, CrmApiFactory factory, Guid customerId,
        string subject = "Invoice shows wrong amount", string category = "General inquiry", string priority = "Medium", Guid? assigneeId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tickets", new
        {
            subject,
            description = "The customer reports a problem that needs follow-up.",
            customerId,
            categoryId = await factory.CategoryId(category),
            priority,
            channel = "Phone",
            assigneeId,
        }, CrmApiFactory.Json);
        return await response.ReadJson(HttpStatusCode.Created);
    }

    public static Guid Id(this JsonElement e) => e.GetProperty("id").GetGuid();

    public static string Str(this JsonElement e, string property) => e.GetProperty(property).GetString()!;
}
