using Crm.Application.Abstractions;
using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates realistic demo customers and tickets spread over the last six weeks, for development and demos.
/// Deterministic (fixed random seed) and idempotent: skipped when demo customers already exist.
/// Tickets are driven through the real domain methods, so statuses, history and timestamps are consistent.
/// </summary>
public sealed class DemoDataSeeder(CrmDbContext db, IClock clock, ILogger<DemoDataSeeder> logger)
{
    /// <summary>Demo customers carry this ERP reference prefix; it doubles as the "already seeded" marker.</summary>
    public const string Marker = "DEMO-";

    private const int Days = 42;
    private readonly Random _random = new(2026);

    private sealed record Staff(Guid Id, string Name, IReadOnlyList<Guid> Departments, bool IsSupervisor);

    private sealed record Topic(string Category, string Subject, string Description, string FirstReply);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.ErpReference != null && c.ErpReference.StartsWith(Marker), ct))
        {
            logger.LogInformation("Demo data already present; skipping");
            return;
        }

        var staff = await LoadStaff(ct);
        var categories = await db.Categories.Where(c => c.IsActive).ToDictionaryAsync(c => c.NameEn, ct);
        var branchId = await db.Branches.Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        if (staff.Count == 0 || categories.Count == 0)
        {
            logger.LogWarning("Demo data needs seeded agents and categories (Seed:SampleData); skipping");
            return;
        }

        var now = clock.UtcNow;
        var customers = CreateCustomers(branchId, now);
        db.Customers.AddRange(customers);
        db.ContactPeople.AddRange(customers.SelectMany(c => c.Contacts));
        await db.SaveChangesAsync(ct);

        var tickets = 0;
        var messages = 0;
        // Oldest first so TCK reference numbers increase with time.
        foreach (var plan in PlanTickets(customers, now).OrderBy(p => p.CreatedAt))
        {
            var (ticket, ticketMessages) = BuildTicket(plan, categories, staff, now);
            db.Tickets.Add(ticket);
            db.Messages.AddRange(ticketMessages);
            tickets++;
            messages += ticketMessages.Count;
        }

        await db.SaveChangesAsync(ct);
        AddCustomerNotes(customers, staff, now);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded demo data: {Customers} customers, {Tickets} tickets, {Messages} messages", customers.Count, tickets, messages);
    }

    /// <summary>Deletes demo customers and everything attached to them. Data entered by users is kept.</summary>
    public async Task RemoveAsync(CancellationToken ct = default)
    {
        var demoCustomers = db.Customers.IgnoreQueryFilters().Where(c => c.ErpReference != null && c.ErpReference.StartsWith(Marker)).Select(c => c.Id);
        var demoTickets = db.Tickets.IgnoreQueryFilters().Where(t => demoCustomers.Contains(t.CustomerId)).Select(t => t.Id);

        // Messages, mentions and history cascade from tickets; contacts and notes cascade from customers.
        var tickets = await db.Tickets.IgnoreQueryFilters().Where(t => demoTickets.Contains(t.Id)).ExecuteDeleteAsync(ct);
        var customers = await db.Customers.IgnoreQueryFilters().Where(c => demoCustomers.Contains(c.Id)).ExecuteDeleteAsync(ct);
        logger.LogInformation("Removed demo data: {Customers} customers, {Tickets} tickets", customers, tickets);
    }

    // ------------------------------------------------------------------ customers

    private List<Customer> CreateCustomers(Guid? branchId, DateTimeOffset now)
    {
        var companies = new (string Name, string Email, string Phone, string City, Language Lang, (string Name, string Email, string Title)[] Contacts)[]
        {
            ("شركة النخبة للتجارة", "info@nukhba.sa", "+966112345678", "الرياض", Language.Ar,
                [("خالد العتيبي", "khalid@nukhba.sa", "مدير المشتريات"), ("ريم السعود", "reem@nukhba.sa", "المالية")]),
            ("Gulf Logistics Co.", "support@gulflog.com", "+966138812200", "Dammam", Language.En,
                [("James Carter", "james@gulflog.com", "Operations lead"), ("Fahad Al-Dosari", "fahad@gulflog.com", "IT manager")]),
            ("مؤسسة الأفق للمقاولات", "contact@alofuq.sa", "+966126650011", "جدة", Language.Ar,
                [("سلمان الغامدي", "salman@alofuq.sa", "مدير المشاريع")]),
            ("Al Waha Medical Center", "admin@alwaha-med.sa", "+966114470090", "Riyadh", Language.En,
                [("Dr. Hana Yousef", "hana@alwaha-med.sa", "Clinic director"), ("Omar Nasser", "omar.n@alwaha-med.sa", "Procurement")]),
            ("شركة رواد التقنية", "hello@rowad.tech", "+966115551234", "الرياض", Language.Ar,
                [("نورة القحطاني", "noura@rowad.tech", "مديرة الحسابات")]),
            ("Red Sea Hospitality Group", "reservations@redsea-hg.com", "+966122214400", "Jeddah", Language.En,
                [("Laura Mendes", "laura@redsea-hg.com", "Guest relations"), ("Majed Al-Harthi", "majed@redsea-hg.com", "Finance")]),
            ("مصنع الجزيرة للبلاستيك", "sales@jazeera-plastic.sa", "+966138899001", "الدمام", Language.Ar,
                [("عبدالله الشمري", "abdullah@jazeera-plastic.sa", "مدير المبيعات")]),
            ("Najd Retail Holdings", "cs@najdretail.sa", "+966112009988", "Riyadh", Language.En,
                [("Sara Al-Mutairi", "sara@najdretail.sa", "Store operations")]),
            ("شركة المدار للاتصالات", "support@madar.sa", "+966114400500", "الرياض", Language.Ar,
                [("فيصل الحربي", "faisal@madar.sa", "الدعم الفني"), ("لمى الزهراني", "lama@madar.sa", "العقود")]),
            ("Eastern Energy Services", "info@eastern-energy.com", "+966133321100", "Khobar", Language.En,
                [("Ahmed Farouk", "ahmed@eastern-energy.com", "HSE manager")]),
        };

        var individuals = new (string Name, string? Email, string? Phone, Language Lang, bool NeedsReview)[]
        {
            ("سارة القحطاني", null, "+966555443322", Language.Ar, false),
            ("Ahmed Mansour", "ahmed.m@mail.com", "+966501234001", Language.En, false),
            ("محمد العنزي", "m.alanazi@gmail.com", "+966502223344", Language.Ar, false),
            ("Fatima Al-Zahrani", "fatima.z@outlook.com", null, Language.En, false),
            ("عبدالرحمن السبيعي", null, "+966533219876", Language.Ar, false),
            ("Yousef Haddad", "yousef.haddad@mail.com", "+966544110022", Language.En, false),
            ("هيفاء المالكي", "haifa.m@gmail.com", "+966566778899", Language.Ar, false),
            ("Khalid Rahman", "k.rahman@proton.me", null, Language.En, false),
            ("ريم الدوسري", null, "+966598887766", Language.Ar, false),
            ("Nour Saleh", "nour.saleh@mail.com", "+966507770011", Language.En, false),
            ("تركي البقمي", "turki.b@gmail.com", "+966551239900", Language.Ar, false),
            ("Mariam Khan", "mariam.khan@mail.com", "+966509991122", Language.En, false),
            ("عميل جديد من واتساب", null, "+966581112233", Language.Ar, true),
            ("unknown.sender@mail.com", "unknown.sender@mail.com", null, Language.En, true),
        };

        var result = new List<Customer>();
        var n = 1000;
        foreach (var c in companies)
        {
            var customer = new Customer(CustomerType.Company, c.Name, c.Email, c.Phone, c.Lang, branchId);
            customer.UpdateProfile(CustomerType.Company, c.Lang, branchId, new Address(null, c.City, "SA"), $"{Marker}{++n}");
            foreach (var p in c.Contacts)
            {
                customer.AddContact(p.Name, p.Email, null, p.Title, isPrimary: false);
            }

            customer.CreatedAt = now.AddDays(-Days - _random.Next(5, 60));
            result.Add(customer);
        }

        foreach (var p in individuals)
        {
            var customer = p.NeedsReview
                ? Customer.CreateFromUnknownSender(p.Name, p.Email, p.Phone, p.Lang)
                : new Customer(CustomerType.Individual, p.Name, p.Email, p.Phone, p.Lang, branchId);
            customer.UpdateProfile(CustomerType.Individual, p.Lang, branchId, null, $"{Marker}{++n}");
            if (!p.NeedsReview)
            {
                customer.MarkReviewed();
            }

            customer.CreatedAt = p.NeedsReview ? now.AddDays(-_random.Next(0, 3)) : now.AddDays(-Days - _random.Next(1, 90));
            result.Add(customer);
        }

        return result;
    }

    // ------------------------------------------------------------------ tickets

    private sealed record TicketPlan(Customer Customer, Topic Topic, TicketPriority Priority, Channel Channel, TicketStatus Target, DateTimeOffset CreatedAt, bool Escalate);

    private List<TicketPlan> PlanTickets(List<Customer> customers, DateTimeOffset now)
    {
        var plans = new List<TicketPlan>();
        var topicsAr = TopicsAr();
        var topicsEn = TopicsEn();

        for (var i = 0; i < 96; i++)
        {
            var customer = customers[_random.Next(customers.Count)];
            var topics = customer.PreferredLanguage == Language.Ar ? topicsAr : topicsEn;
            var topic = topics[_random.Next(topics.Length)];

            // Recent days are busier; a handful of tickets arrive today so the desk has live work.
            var ageDays = i < 8 ? 0 : (int)Math.Floor(Math.Pow(_random.NextDouble(), 1.6) * Days);
            var createdAt = now.AddDays(-ageDays).AddMinutes(-_random.Next(10, ageDays == 0 ? 360 : 600));

            // Older tickets are mostly finished; new ones are mostly open.
            var target = ageDays switch
            {
                0 => Pick((TicketStatus.New, 4), (TicketStatus.Open, 3), (TicketStatus.InProgress, 2), (TicketStatus.Resolved, 1)),
                <= 3 => Pick((TicketStatus.New, 1), (TicketStatus.Open, 3), (TicketStatus.InProgress, 3), (TicketStatus.PendingCustomer, 2), (TicketStatus.Resolved, 1)),
                <= 10 => Pick((TicketStatus.Open, 1), (TicketStatus.InProgress, 2), (TicketStatus.PendingCustomer, 2), (TicketStatus.Resolved, 3), (TicketStatus.Closed, 2)),
                _ => Pick((TicketStatus.InProgress, 1), (TicketStatus.Resolved, 2), (TicketStatus.Closed, 7)),
            };

            var priority = Pick((TicketPriority.Urgent, 1), (TicketPriority.High, 3), (TicketPriority.Medium, 5), (TicketPriority.Low, 2));
            var channel = customer.PrimaryEmail is null
                ? Pick((Channel.WhatsApp, 5), (Channel.Phone, 3), (Channel.Sms, 1))
                : Pick((Channel.Email, 5), (Channel.Phone, 2), (Channel.WhatsApp, 2), (Channel.WebForm, 1), (Channel.Portal, 1));

            plans.Add(new TicketPlan(customer, topic, priority, channel, target, createdAt,
                Escalate: priority >= TicketPriority.High && _random.NextDouble() < 0.18));
        }

        return plans;
    }

    private (Ticket Ticket, List<Message> Messages) BuildTicket(
        TicketPlan p, Dictionary<string, Category> categories, List<Staff> staff, DateTimeOffset now)
    {
        var category = categories.GetValueOrDefault(p.Topic.Category) ?? categories.Values.First();
        var departmentId = category.DepartmentId ?? staff[0].Departments[0];
        var departmentAgents = staff.Where(s => !s.IsSupervisor && s.Departments.Contains(departmentId)).ToList();
        var supervisor = staff.FirstOrDefault(s => s.IsSupervisor) ?? staff[0];
        var agent = departmentAgents.Count > 0 ? departmentAgents[_random.Next(departmentAgents.Count)] : supervisor;
        var contactId = p.Customer.Contacts.Count > 0 && _random.NextDouble() < 0.7
            ? p.Customer.Contacts.ElementAt(_random.Next(p.Customer.Contacts.Count)).Id
            : (Guid?)null;

        var createdBy = p.Channel is Channel.Phone ? agent.Id : (Guid?)null;
        var t = p.CreatedAt;
        var ticket = Ticket.Create(p.Topic.Subject, p.Topic.Description, p.Customer.Id, contactId, p.Channel,
            category.Id, p.Priority, departmentId, p.Customer.BranchId, createdBy, t);
        var messages = new List<Message>();
        // Each step moves the ticket clock forward but never past the present.
        DateTimeOffset Step(int minMinutes, int maxMinutes) => t = Min(t.AddMinutes(_random.Next(minMinutes, maxMinutes)), now.AddMinutes(-2));

        if (p.Channel is not Channel.Phone)
        {
            messages.Add(Message.CustomerMessage(ticket.Id, p.Channel, p.Topic.Description, p.Customer.Id, t));
        }

        if (p.Target == TicketStatus.New)
        {
            return Finish(ticket, messages, t, null);
        }

        // Most tickets are picked up by an agent; a few are assigned by the supervisor.
        if (_random.NextDouble() < 0.7)
        {
            ticket.Take(agent.Id, Step(5, 90));
        }
        else
        {
            ticket.Assign(agent.Id, supervisor.Id, Step(5, 120));
        }

        if (p.Target == TicketStatus.Open && _random.NextDouble() < 0.5)
        {
            return Finish(ticket, messages, t, agent.Id);
        }

        var reply = Message.AgentReply(ticket.Id, ticket.Channel, p.Topic.FirstReply, agent.Id, Step(10, 180));
        reply.MarkDelivery(p.Channel == Channel.Email ? DeliveryStatus.Delivered : DeliveryStatus.Sent);
        ticket.RecordAgentReply(agent.Id, t);
        messages.Add(reply);

        if (_random.NextDouble() < 0.35)
        {
            var colleague = staff.Where(s => s.Id != agent.Id).OrderBy(_ => _random.Next()).First();
            messages.Add(Message.InternalNote(ticket.Id, InternalNoteText(p.Customer.PreferredLanguage, colleague.Name), agent.Id, [colleague.Id], Step(20, 240)));
        }

        if (p.Escalate && ticket.Status != TicketStatus.Closed)
        {
            ticket.Escalate(p.Customer.PreferredLanguage == Language.Ar ? "العميل من كبار العملاء وينتظر ردًا عاجلًا" : "Key account waiting on an urgent answer",
                supervisor.Id, agent.Id, Step(30, 300));
        }

        switch (p.Target)
        {
            case TicketStatus.Open:
                break;
            case TicketStatus.InProgress:
                ticket.ChangeStatus(TicketStatus.InProgress, agent.Id, Step(10, 240));
                break;
            case TicketStatus.PendingCustomer:
                ticket.ChangeStatus(TicketStatus.PendingCustomer, agent.Id, Step(10, 240));
                break;
            case TicketStatus.Resolved or TicketStatus.Closed:
                ticket.ChangeStatus(TicketStatus.InProgress, agent.Id, Step(10, 240));
                messages.Add(Message.CustomerMessage(ticket.Id, ticket.Channel, CustomerFollowUp(p.Customer.PreferredLanguage), p.Customer.Id, Step(60, 900)));
                var closingReply = Message.AgentReply(ticket.Id, ticket.Channel, ResolutionText(p.Customer.PreferredLanguage), ticket.AssigneeId ?? agent.Id, Step(30, 600));
                closingReply.MarkDelivery(DeliveryStatus.Delivered);
                messages.Add(closingReply);
                ticket.ChangeStatus(TicketStatus.Resolved, ticket.AssigneeId ?? agent.Id, t);
                if (p.Target == TicketStatus.Closed)
                {
                    ticket.ChangeStatus(TicketStatus.Closed, ticket.AssigneeId ?? agent.Id, Step(600, 4000));
                }

                break;
        }

        return Finish(ticket, messages, t, ticket.AssigneeId ?? agent.Id);
    }

    private static (Ticket, List<Message>) Finish(Ticket ticket, List<Message> messages, DateTimeOffset lastActivity, Guid? by)
    {
        if (lastActivity > ticket.CreatedAt)
        {
            ticket.UpdatedAt = lastActivity;
            ticket.UpdatedBy = by;
        }

        return (ticket, messages);
    }

    private void AddCustomerNotes(List<Customer> customers, List<Staff> staff, DateTimeOffset now)
    {
        var notesAr = new[]
        {
            "العميل يفضّل التواصل عبر البريد الإلكتروني صباحًا.",
            "تم الاتفاق على مراجعة شهرية للحساب مع مدير المشتريات.",
            "يرجى التأكد من إرسال الفواتير باللغة العربية.",
        };
        var notesEn = new[]
        {
            "Prefers WhatsApp for quick updates; formal replies by email.",
            "Renewal due next quarter — flag any service issues to the account manager.",
            "Invoices must reference the PO number on every line.",
        };

        foreach (var customer in customers.Where(c => c.Type == CustomerType.Company || _random.NextDouble() < 0.3))
        {
            var pool = customer.PreferredLanguage == Language.Ar ? notesAr : notesEn;
            var author = staff[_random.Next(staff.Count)];
            var note = new Note(customer.Id, pool[_random.Next(pool.Length)], author.Id) { CreatedAt = now.AddDays(-_random.Next(1, Days)) };
            db.Notes.Add(note);
        }
    }

    private async Task<List<Staff>> LoadStaff(CancellationToken ct)
    {
        var supervisorRole = await db.Roles.Where(r => r.Name == Application.Common.Security.SystemRoles.Supervisor).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var users = await db.Users.Include(u => u.Departments)
            .Where(u => u.UserType == UserType.Staff && u.IsActive && u.Departments.Count > 0)
            .ToListAsync(ct);
        var supervisors = await db.UserRoles.Where(r => r.RoleId == supervisorRole).Select(r => r.UserId).ToListAsync(ct);

        return users
            .Where(u => u.Email != "admin@crm.local")
            .Select(u => new Staff(u.Id, u.FullName, u.Departments.Select(d => d.DepartmentId).ToList(), supervisors.Contains(u.Id)))
            .ToList();
    }

    private T Pick<T>(params (T Value, int Weight)[] options)
    {
        var roll = _random.Next(options.Sum(o => o.Weight));
        foreach (var (value, weight) in options)
        {
            if ((roll -= weight) < 0)
            {
                return value;
            }
        }

        return options[^1].Value;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    // ------------------------------------------------------------------ content

    private static string InternalNoteText(Language lang, string colleague) => lang == Language.Ar
        ? $"@{colleague} هل يمكنك مراجعة هذه الحالة؟ العميل ذكر أن المشكلة تكررت الأسبوع الماضي."
        : $"@{colleague} can you double-check this one? The customer says it happened last week too.";

    private static string CustomerFollowUp(Language lang) => lang == Language.Ar
        ? "شكرًا لكم، هل هناك موعد متوقع للحل؟"
        : "Thanks for the update. Do you have an expected time for the fix?";

    private static string ResolutionText(Language lang) => lang == Language.Ar
        ? "تم حل المشكلة. نرجو تأكيد أن كل شيء يعمل لديكم الآن، وسنغلق التذكرة خلال أيام إذا لم تصلنا ملاحظات."
        : "This is now fixed. Please confirm everything works on your side; we'll close the ticket in a few days if we don't hear back.";

    private static Topic[] TopicsAr() =>
    [
        new("Billing & payments", "فاتورة الشهر تحتوي على مبلغ غير صحيح", "لاحظنا أن فاتورة هذا الشهر تتضمن رسوم شحن مكررة. نرجو مراجعتها وإرسال فاتورة مصححة.", "شكرًا لتواصلكم. نراجع الفاتورة الآن مع قسم الفوترة وسنرسل فاتورة مصححة خلال يوم عمل."),
        new("Billing & payments", "طلب استرداد مبلغ مدفوع مرتين", "تم خصم مبلغ الاشتراك مرتين من البطاقة بتاريخ اليوم. نرجو استرداد المبلغ المكرر.", "نعتذر عن الإزعاج. تأكدنا من الخصم المكرر وبدأنا إجراءات الاسترداد، وسيظهر المبلغ خلال ٥ أيام عمل."),
        new("Technical issue", "تعذّر تسجيل الدخول إلى بوابة العملاء", "عند محاولة تسجيل الدخول تظهر رسالة خطأ بعد إدخال كلمة المرور مباشرة.", "شكرًا للإبلاغ. أعدنا تعيين الجلسة من جهتنا، نرجو المحاولة مرة أخرى وإبلاغنا بالنتيجة."),
        new("Technical issue", "التطبيق يتوقف عند رفع المستندات", "يتوقف التطبيق على الجوال عند محاولة رفع ملف PDF أكبر من ٥ ميجابايت.", "تمكنا من إعادة إنتاج المشكلة، وفريق التطوير يعمل على إصلاحها. سنوافيكم بالتحديث قريبًا."),
        new("Complaint", "تأخر وصول الشحنة عن الموعد المحدد", "الطلب كان من المفترض أن يصل قبل ثلاثة أيام ولم يصل حتى الآن، ولا يوجد تحديث في التتبع.", "نعتذر عن التأخير. تواصلنا مع شركة الشحن وسنرسل لكم موعد التسليم الجديد اليوم."),
        new("Complaint", "سوء تعامل من موظف الاستقبال", "واجهنا تعاملًا غير لائق أثناء زيارتنا للفرع يوم الأحد، ونرجو التحقيق في الأمر.", "نشكركم على ملاحظتكم ونأسف لما حدث. تم رفع الموضوع لمدير الفرع وسنتواصل معكم بالنتيجة."),
        new("General inquiry", "استفسار عن سياسة الاسترجاع", "ما هي المدة المسموحة لإرجاع المنتجات، وهل يمكن الاسترجاع بدون الفاتورة الأصلية؟", "يمكن إرجاع المنتجات خلال ١٤ يومًا من الشراء، ويكفي رقم الطلب في حال عدم توفر الفاتورة."),
        new("General inquiry", "طلب تحديث بيانات السجل التجاري", "نرغب في تحديث بيانات السجل التجاري والعنوان الوطني في حسابنا.", "نرجو إرسال نسخة من السجل التجاري المحدث وسنقوم بتحديث البيانات فور استلامها."),
        new("Feature request", "إضافة خيار الدفع عبر مدى في البوابة", "نرغب في إضافة خيار الدفع ببطاقة مدى مباشرة من بوابة العملاء.", "شكرًا لاقتراحكم. تمت إضافته إلى خطة التطوير وسنبلغكم عند توفره."),
    ];

    private static Topic[] TopicsEn() =>
    [
        new("Billing & payments", "Invoice shows the wrong VAT amount", "Our latest invoice applies VAT twice on the service line. Please review and reissue.", "Thanks for flagging this. Billing is reviewing the invoice now and we'll send a corrected copy within one business day."),
        new("Billing & payments", "Payment not reflected on account", "We paid invoice INV-2291 by bank transfer on Sunday but the portal still shows it as unpaid.", "We've located your transfer and are matching it to the invoice. The status will update by end of day."),
        new("Technical issue", "Portal login fails with error 403", "Several of our users get a 403 error right after signing in to the portal.", "We've reset the affected accounts. Please try again and let us know if any user still sees the error."),
        new("Technical issue", "Reports export is timing out", "Exporting the monthly report to Excel times out after about a minute.", "We reproduced the timeout and the team is working on a fix. As a workaround, export one week at a time."),
        new("Technical issue", "API returns 500 on order sync", "Our ERP integration gets HTTP 500 when syncing orders since this morning.", "Thanks — we found a failing job on our side and restarted it. Please retry the sync and confirm."),
        new("Complaint", "Delivery arrived damaged", "Two of the five boxes in yesterday's delivery arrived damaged. Photos attached to the email.", "We're sorry about this. A replacement has been arranged and the courier will collect the damaged boxes."),
        new("General inquiry", "Request for bulk shipping quote", "Could you send a quote for 40 pallets to Dammam per month starting next quarter?", "Happy to help. Our sales team will prepare the quote and send it to you by Thursday."),
        new("General inquiry", "Change of billing contact", "Please update our billing contact to finance@ our domain from next month.", "Done — the billing contact is updated and future invoices will go to the new address."),
        new("Feature request", "Allow bulk user import", "We'd like to import our 200 users from a CSV instead of adding them one by one.", "Thanks for the suggestion. It's on our roadmap and we'll let you know when it's available."),
    ];
}
