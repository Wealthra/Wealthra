using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wealthra.Domain.Constants;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Persistence.Seeding
{
    /// <summary>
    /// Seeds global categories once, then per-user demo data (incomes, expenses, budgets, goals).
    /// Safe to call multiple times — skips demo seeding if the user already has expenses.
    ///
    /// NOTE: ApplicationDbContext.SaveChangesAsync overrides CreatedBy with 'System' when
    /// there is no HTTP context. We correct this immediately after each save with a raw SQL UPDATE.
    /// </summary>
    public static class DataSeeder
    {
        /// <summary>Inserts the standard bilingual category set when the table is empty.</summary>
        public static async Task EnsureGlobalCategoriesAsync(ApplicationDbContext db)
        {
            if (await db.Categories.AnyAsync()) return;

            var categories = StandardGlobalCategoryPairs.Pairs
                .Select(p => new Category(p.NameEn, p.NameTr))
                .ToArray();

            await db.Categories.AddRangeAsync(categories);
            await db.SaveChangesAsync();
        }

        private static async Task<IReadOnlyDictionary<string, Category>> GetCategoryMapAsync(ApplicationDbContext db) =>
            await db.Categories.ToDictionaryAsync(c => c.NameEn);

        public static async Task SeedDemoDataAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await EnsureGlobalCategoriesAsync(db);

            var user = await userManager.FindByEmailAsync("user@wealthra.local");
            if (user == null) return;

            var userId = user.Id;

            if (await db.Expenses.AnyAsync(e => e.CreatedBy == userId)) return;

            var now = DateTime.UtcNow;

            var cat = await GetCategoryMapAsync(db);
            var food = cat["Food & Dining"];
            var transport = cat["Transport"];
            var housing = cat["Housing"];
            var health = cat["Health & Fitness"];
            var entertain = cat["Entertainment"];
            var shopping = cat["Shopping"];
            var utilities = cat["Utilities"];
            var education = cat["Education"];

            // ─────────────────────────────────────────────────────────────
            // 2. BUDGETS (monthly limits per category)
            // ─────────────────────────────────────────────────────────────
            var budgets = new[]
            {
                new Budget(food.Id,       600m),
                new Budget(transport.Id,  300m),
                new Budget(housing.Id,   1200m),
                new Budget(health.Id,     150m),
                new Budget(entertain.Id,  200m),
                new Budget(shopping.Id,   250m),
                new Budget(utilities.Id,  200m),
                new Budget(education.Id,  100m),
            };

            await db.Budgets.AddRangeAsync(budgets);
            await db.SaveChangesAsync();
            await FixCreatedBy(db, "Budgets", userId);

            // ─────────────────────────────────────────────────────────────
            // 3. INCOMES — spread across 3 months
            // ─────────────────────────────────────────────────────────────
            var incomes = new List<Income>
            {
                // Current month
                MakeIncome("Monthly Salary",        4500m, "Bank Transfer", true,  MonthDay(now,  0,  1)),
                MakeIncome("Freelance Web Project",  800m, "Bank Transfer", false, MonthDay(now,  0,  8)),
                MakeIncome("Stock Dividends",        120m, "Bank Transfer", false, MonthDay(now,  0, 10)),

                // Last month
                MakeIncome("Monthly Salary",        4500m, "Bank Transfer", true,  MonthDay(now, -1,  1)),
                MakeIncome("Freelance Logo Design",  350m, "Bank Transfer", false, MonthDay(now, -1, 15)),
                MakeIncome("Bonus Payment",          500m, "Bank Transfer", false, MonthDay(now, -1, 20)),

                // Two months ago
                MakeIncome("Monthly Salary",        4500m, "Bank Transfer", true,  MonthDay(now, -2,  1)),
                MakeIncome("Tutoring Sessions",      200m, "Cash",          false, MonthDay(now, -2, 12)),
            };

            await db.Incomes.AddRangeAsync(incomes);
            await db.SaveChangesAsync();
            await FixCreatedBy(db, "Incomes", userId);

            // ─────────────────────────────────────────────────────────────
            // 4. EXPENSES — this month + 2 prior months
            // ─────────────────────────────────────────────────────────────
            var expenses = new List<Expense>
            {
                // ── This Month ──
                MakeExpense("Supermarket Weekly Shop",   85m, "Debit Card",    food.Id,      false, MonthDay(now,  0,  2)),
                MakeExpense("Restaurant — Dinner",       62m, "Credit Card",   food.Id,      false, MonthDay(now,  0,  5)),
                MakeExpense("Supermarket Weekly Shop",   91m, "Debit Card",    food.Id,      false, MonthDay(now,  0,  9)),
                MakeExpense("Coffee Subscription",       18m, "Debit Card",    food.Id,      true,  MonthDay(now,  0,  1)),
                MakeExpense("Takeaway Pizza",            24m, "Credit Card",   food.Id,      false, MonthDay(now,  0, 13)),
                MakeExpense("Monthly Transit Pass",      95m, "Debit Card",    transport.Id, true,  MonthDay(now,  0,  1)),
                MakeExpense("Fuel",                      55m, "Credit Card",   transport.Id, false, MonthDay(now,  0,  7)),
                MakeExpense("Uber — Airport",            38m, "Credit Card",   transport.Id, false, MonthDay(now,  0, 11)),
                MakeExpense("Monthly Rent",            1100m, "Bank Transfer", housing.Id,   true,  MonthDay(now,  0,  1)),
                MakeExpense("Gym Membership",            45m, "Debit Card",    health.Id,    true,  MonthDay(now,  0,  1)),
                MakeExpense("Pharmacy",                  28m, "Cash",          health.Id,    false, MonthDay(now,  0,  6)),
                MakeExpense("Netflix",                   15m, "Credit Card",   entertain.Id, true,  MonthDay(now,  0,  1)),
                MakeExpense("Cinema Tickets",            32m, "Credit Card",   entertain.Id, false, MonthDay(now,  0,  9)),
                MakeExpense("Spotify",                   10m, "Credit Card",   entertain.Id, true,  MonthDay(now,  0,  1)),
                MakeExpense("Clothing — Jacket",         89m, "Credit Card",   shopping.Id,  false, MonthDay(now,  0,  4)),
                MakeExpense("Amazon — Books",            34m, "Credit Card",   shopping.Id,  false, MonthDay(now,  0,  8)),
                MakeExpense("Electricity Bill",          72m, "Bank Transfer", utilities.Id, true,  MonthDay(now,  0,  3)),
                MakeExpense("Internet Bill",             45m, "Bank Transfer", utilities.Id, true,  MonthDay(now,  0,  3)),
                MakeExpense("Udemy Course",              29m, "Credit Card",   education.Id, false, MonthDay(now,  0, 10)),

                // ── Last Month ──
                MakeExpense("Supermarket Weekly Shop",   78m, "Debit Card",    food.Id,      false, MonthDay(now, -1,  3)),
                MakeExpense("Restaurant — Lunch",        45m, "Credit Card",   food.Id,      false, MonthDay(now, -1, 10)),
                MakeExpense("Supermarket Weekly Shop",   83m, "Debit Card",    food.Id,      false, MonthDay(now, -1, 17)),
                MakeExpense("Supermarket Weekly Shop",   72m, "Debit Card",    food.Id,      false, MonthDay(now, -1, 24)),
                MakeExpense("Coffee Subscription",       18m, "Debit Card",    food.Id,      true,  MonthDay(now, -1,  1)),
                MakeExpense("Monthly Transit Pass",      95m, "Debit Card",    transport.Id, true,  MonthDay(now, -1,  1)),
                MakeExpense("Fuel",                      48m, "Credit Card",   transport.Id, false, MonthDay(now, -1, 15)),
                MakeExpense("Monthly Rent",            1100m, "Bank Transfer", housing.Id,   true,  MonthDay(now, -1,  1)),
                MakeExpense("Gym Membership",            45m, "Debit Card",    health.Id,    true,  MonthDay(now, -1,  1)),
                MakeExpense("Doctor Visit",              60m, "Cash",          health.Id,    false, MonthDay(now, -1,  8)),
                MakeExpense("Netflix",                   15m, "Credit Card",   entertain.Id, true,  MonthDay(now, -1,  1)),
                MakeExpense("Spotify",                   10m, "Credit Card",   entertain.Id, true,  MonthDay(now, -1,  1)),
                MakeExpense("Concert Tickets",           75m, "Credit Card",   entertain.Id, false, MonthDay(now, -1, 20)),
                MakeExpense("Electricity Bill",          68m, "Bank Transfer", utilities.Id, true,  MonthDay(now, -1,  3)),
                MakeExpense("Internet Bill",             45m, "Bank Transfer", utilities.Id, true,  MonthDay(now, -1,  3)),
                MakeExpense("Online Course Renewal",     49m, "Credit Card",   education.Id, false, MonthDay(now, -1,  5)),
                MakeExpense("Shoe Purchase",            110m, "Credit Card",   shopping.Id,  false, MonthDay(now, -1, 12)),

                // ── Two Months Ago ──
                MakeExpense("Supermarket Weekly Shop",   80m, "Debit Card",    food.Id,      false, MonthDay(now, -2,  4)),
                MakeExpense("Restaurant — Dinner",       70m, "Credit Card",   food.Id,      false, MonthDay(now, -2, 11)),
                MakeExpense("Supermarket Weekly Shop",   76m, "Debit Card",    food.Id,      false, MonthDay(now, -2, 18)),
                MakeExpense("Coffee Subscription",       18m, "Debit Card",    food.Id,      true,  MonthDay(now, -2,  1)),
                MakeExpense("Monthly Transit Pass",      95m, "Debit Card",    transport.Id, true,  MonthDay(now, -2,  1)),
                MakeExpense("Taxi",                      22m, "Cash",          transport.Id, false, MonthDay(now, -2,  7)),
                MakeExpense("Monthly Rent",            1100m, "Bank Transfer", housing.Id,   true,  MonthDay(now, -2,  1)),
                MakeExpense("Gym Membership",            45m, "Debit Card",    health.Id,    true,  MonthDay(now, -2,  1)),
                MakeExpense("Netflix",                   15m, "Credit Card",   entertain.Id, true,  MonthDay(now, -2,  1)),
                MakeExpense("Spotify",                   10m, "Credit Card",   entertain.Id, true,  MonthDay(now, -2,  1)),
                MakeExpense("Gaming Console Game",       59m, "Credit Card",   entertain.Id, false, MonthDay(now, -2, 15)),
                MakeExpense("Electricity Bill",          74m, "Bank Transfer", utilities.Id, true,  MonthDay(now, -2,  3)),
                MakeExpense("Internet Bill",             45m, "Bank Transfer", utilities.Id, true,  MonthDay(now, -2,  3)),
                MakeExpense("Winter Clothing",          145m, "Credit Card",   shopping.Id,  false, MonthDay(now, -2, 20)),
            };

            await db.Expenses.AddRangeAsync(expenses);
            await db.SaveChangesAsync();
            await FixCreatedBy(db, "Expenses", userId);

            // Update budgets' CurrentAmount with this month's actual expense totals
            var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (var budget in budgets)
            {
                var spent = expenses
                    .Where(e => e.CategoryId == budget.CategoryId && e.TransactionDate >= thisMonthStart)
                    .Sum(e => e.Amount);

                if (spent > 0)
                    budget.AddExpense(spent);
            }

            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE \"Budgets\" SET \"LastModifiedBy\" = '{userId}' WHERE \"LastModifiedBy\" = 'System'");

            // ─────────────────────────────────────────────────────────────
            // 5. GOALS
            // ─────────────────────────────────────────────────────────────
            var goals = new[]
            {
                new Goal { Name = "Emergency Fund",         TargetAmount = 10000m, CurrentAmount = 3200m, Deadline = now.AddMonths(8) },
                new Goal { Name = "New Laptop",             TargetAmount =  1500m, CurrentAmount =  900m, Deadline = now.AddMonths(3) },
                new Goal { Name = "Summer Vacation — Italy",TargetAmount =  3000m, CurrentAmount =  750m, Deadline = now.AddMonths(6) },
                new Goal { Name = "Retirement Savings",     TargetAmount = 50000m, CurrentAmount = 8500m, Deadline = now.AddYears(10) },
                new Goal { Name = "Home Down Payment",      TargetAmount = 25000m, CurrentAmount = 4200m, Deadline = now.AddYears(3)  },
            };

            await db.Goals.AddRangeAsync(goals);
            await db.SaveChangesAsync();
            await FixCreatedBy(db, "Goals", userId);
        }

        /// <summary>
        /// Seeds data for anomaly detection: anomalous.user (triggers alerts) and stable.user (no alerts).
        /// Safe to call multiple times — skips if data already exists for each user.
        /// </summary>
        public static async Task SeedAnomalyDetectionDataAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var now = DateTime.UtcNow;

            // ─── Anomalous user: Shopping >30% of income, Entertainment >50% MoM spike ───
            await EnsureGlobalCategoriesAsync(db);
            var cat = await GetCategoryMapAsync(db);

            var anomalousUser = await userManager.FindByEmailAsync("anomalous.user@wealthra.local");
            if (anomalousUser != null && !await db.Expenses.AnyAsync(e => e.CreatedBy == anomalousUser.Id))
            {
                var food = cat["Food & Dining"];
                var transport = cat["Transport"];
                var housing = cat["Housing"];
                var health = cat["Health & Fitness"];
                var entertain = cat["Entertainment"];
                var shopping = cat["Shopping"];
                var utilities = cat["Utilities"];
                var education = cat["Education"];

                // Income: 5000 this month, 5000 last month, 5000 two months ago
                var anomalousIncomes = new List<Income>
                {
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, 0, 1)),
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, -1, 1)),
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, -2, 1)),
                };
                await db.Incomes.AddRangeAsync(anomalousIncomes);
                await db.SaveChangesAsync();
                await FixCreatedBy(db, "Incomes", anomalousUser.Id);

                // Expenses: Shopping = 2000 this month (40% of 5000 → triggers >30% rule); Entertainment 100 last month, 210 this month (>50% spike)
                var anomalousExpenses = new List<Expense>
                {
                    // This month
                    MakeExpense("Groceries", 300m, "Debit Card", food.Id, false, MonthDay(now, 0, 5)),
                    MakeExpense("Transit", 95m, "Debit Card", transport.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Rent", 1200m, "Bank Transfer", housing.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Gym", 45m, "Debit Card", health.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Streaming + concert", 210m, "Credit Card", entertain.Id, false, MonthDay(now, 0, 10)), // 210 vs 100 last month = 110% spike
                    MakeExpense("Electronics & clothes", 2000m, "Credit Card", shopping.Id, false, MonthDay(now, 0, 8)),   // 2000/5000 = 40% of income
                    MakeExpense("Bills", 120m, "Bank Transfer", utilities.Id, true, MonthDay(now, 0, 3)),
                    MakeExpense("Course", 50m, "Credit Card", education.Id, false, MonthDay(now, 0, 12)),
                    // Last month
                    MakeExpense("Groceries", 280m, "Debit Card", food.Id, false, MonthDay(now, -1, 5)),
                    MakeExpense("Transit", 95m, "Debit Card", transport.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Rent", 1200m, "Bank Transfer", housing.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Gym", 45m, "Debit Card", health.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Streaming", 100m, "Credit Card", entertain.Id, false, MonthDay(now, -1, 10)),
                    MakeExpense("Clothes", 150m, "Credit Card", shopping.Id, false, MonthDay(now, -1, 8)),
                    MakeExpense("Bills", 115m, "Bank Transfer", utilities.Id, true, MonthDay(now, -1, 3)),
                    MakeExpense("Book", 30m, "Credit Card", education.Id, false, MonthDay(now, -1, 12)),
                    // Two months ago
                    MakeExpense("Groceries", 270m, "Debit Card", food.Id, false, MonthDay(now, -2, 5)),
                    MakeExpense("Transit", 95m, "Debit Card", transport.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Rent", 1200m, "Bank Transfer", housing.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Gym", 45m, "Debit Card", health.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Streaming", 95m, "Credit Card", entertain.Id, false, MonthDay(now, -2, 10)),
                    MakeExpense("Shopping", 140m, "Credit Card", shopping.Id, false, MonthDay(now, -2, 8)),
                    MakeExpense("Bills", 110m, "Bank Transfer", utilities.Id, true, MonthDay(now, -2, 3)),
                    MakeExpense("Course", 40m, "Credit Card", education.Id, false, MonthDay(now, -2, 12)),
                };
                await db.Expenses.AddRangeAsync(anomalousExpenses);
                await db.SaveChangesAsync();
                await FixCreatedBy(db, "Expenses", anomalousUser.Id);
            }

            // ─── Stable user: all categories <30% of income, no >50% MoM spike ───
            var stableUser = await userManager.FindByEmailAsync("stable.user@wealthra.local");
            if (stableUser != null && !await db.Expenses.AnyAsync(e => e.CreatedBy == stableUser.Id))
            {
                var food = cat["Food & Dining"];
                var transport = cat["Transport"];
                var housing = cat["Housing"];
                var health = cat["Health & Fitness"];
                var entertain = cat["Entertainment"];
                var shopping = cat["Shopping"];
                var utilities = cat["Utilities"];
                var education = cat["Education"];

                var stableIncomes = new List<Income>
                {
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, 0, 1)),
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, -1, 1)),
                    MakeIncome("Salary", 5000m, "Bank Transfer", true, MonthDay(now, -2, 1)),
                };
                await db.Incomes.AddRangeAsync(stableIncomes);
                await db.SaveChangesAsync();
                await FixCreatedBy(db, "Incomes", stableUser.Id);

                // Balanced: no category >30%, similar MoM (no spike)
                var stableExpenses = new List<Expense>
                {
                    // This month (~2500 total, each category &lt; 30% of 5000)
                    MakeExpense("Groceries", 400m, "Debit Card", food.Id, false, MonthDay(now, 0, 5)),
                    MakeExpense("Transit", 90m, "Debit Card", transport.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Rent", 1100m, "Bank Transfer", housing.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Gym", 40m, "Debit Card", health.Id, true, MonthDay(now, 0, 1)),
                    MakeExpense("Streaming", 25m, "Credit Card", entertain.Id, false, MonthDay(now, 0, 1)),
                    MakeExpense("Shopping", 120m, "Credit Card", shopping.Id, false, MonthDay(now, 0, 10)),
                    MakeExpense("Bills", 110m, "Bank Transfer", utilities.Id, true, MonthDay(now, 0, 3)),
                    MakeExpense("Course", 45m, "Credit Card", education.Id, false, MonthDay(now, 0, 12)),
                    // Last month (similar totals)
                    MakeExpense("Groceries", 380m, "Debit Card", food.Id, false, MonthDay(now, -1, 5)),
                    MakeExpense("Transit", 90m, "Debit Card", transport.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Rent", 1100m, "Bank Transfer", housing.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Gym", 40m, "Debit Card", health.Id, true, MonthDay(now, -1, 1)),
                    MakeExpense("Streaming", 22m, "Credit Card", entertain.Id, false, MonthDay(now, -1, 1)),
                    MakeExpense("Shopping", 115m, "Credit Card", shopping.Id, false, MonthDay(now, -1, 10)),
                    MakeExpense("Bills", 108m, "Bank Transfer", utilities.Id, true, MonthDay(now, -1, 3)),
                    MakeExpense("Book", 42m, "Credit Card", education.Id, false, MonthDay(now, -1, 12)),
                    // Two months ago
                    MakeExpense("Groceries", 390m, "Debit Card", food.Id, false, MonthDay(now, -2, 5)),
                    MakeExpense("Transit", 90m, "Debit Card", transport.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Rent", 1100m, "Bank Transfer", housing.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Gym", 40m, "Debit Card", health.Id, true, MonthDay(now, -2, 1)),
                    MakeExpense("Streaming", 24m, "Credit Card", entertain.Id, false, MonthDay(now, -2, 1)),
                    MakeExpense("Shopping", 118m, "Credit Card", shopping.Id, false, MonthDay(now, -2, 10)),
                    MakeExpense("Bills", 105m, "Bank Transfer", utilities.Id, true, MonthDay(now, -2, 3)),
                    MakeExpense("Course", 38m, "Credit Card", education.Id, false, MonthDay(now, -2, 12)),
                };
                await db.Expenses.AddRangeAsync(stableExpenses);
                await db.SaveChangesAsync();
                await FixCreatedBy(db, "Expenses", stableUser.Id);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        /// <summary>Corrects CreatedBy/LastModifiedBy that were overwritten with 'System' by the SaveChanges auditing interceptor.</summary>
        private static Task FixCreatedBy(ApplicationDbContext db, string tableName, string userId)
            => db.Database.ExecuteSqlRawAsync(
                $"UPDATE \"{tableName}\" SET \"CreatedBy\" = '{userId}', \"LastModifiedBy\" = '{userId}' WHERE \"CreatedBy\" = 'System'");

        private static Income MakeIncome(string name, decimal amount, string method, bool isRecurring, DateTime date)
            => new Income { Name = name, Amount = amount, Method = method, IsRecurring = isRecurring, TransactionDate = date };

        private static Expense MakeExpense(string description, decimal amount, string paymentMethod, int categoryId, bool isRecurring, DateTime date)
            => new Expense { Description = description, Amount = amount, PaymentMethod = paymentMethod, CategoryId = categoryId, IsRecurring = isRecurring, TransactionDate = date };

        /// <summary>Returns a UTC DateTime at 10:00 on the given day, offset by monthOffset months from now.</summary>
        private static DateTime MonthDay(DateTime now, int monthOffset, int day)
        {
            var target = now.AddMonths(monthOffset);
            var clamped = Math.Min(day, DateTime.DaysInMonth(target.Year, target.Month));
            return new DateTime(target.Year, target.Month, clamped, 10, 0, 0, DateTimeKind.Utc);
        }
    }
}
