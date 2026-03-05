using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Persistence.Seeding
{
    /// <summary>
    /// Seeds rich demo data: categories, incomes, expenses, budgets, and goals.
    /// Safe to call multiple times — skips seeding if data already exists for the user.
    /// 
    /// NOTE: ApplicationDbContext.SaveChangesAsync overrides CreatedBy with 'System' when
    /// there is no HTTP context. We correct this immediately after each save with a raw SQL UPDATE.
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedDemoDataAsync(IServiceProvider serviceProvider)
        {
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync("user@wealthra.local");
            if (user == null) return;

            var userId = user.Id;

            // Guard: skip if already seeded for this user
            if (await db.Categories.AnyAsync(c => c.CreatedBy == userId)) return;

            var now = DateTime.UtcNow;

            // ─────────────────────────────────────────────────────────────
            // 1. CATEGORIES
            // ─────────────────────────────────────────────────────────────
            var categories = new[]
            {
                new Category("Food & Dining"),
                new Category("Transport"),
                new Category("Housing"),
                new Category("Health & Fitness"),
                new Category("Entertainment"),
                new Category("Shopping"),
                new Category("Utilities"),
                new Category("Education"),
            };

            await db.Categories.AddRangeAsync(categories);
            await db.SaveChangesAsync();
            await FixCreatedBy(db, "Categories", userId);

            var food      = categories[0];
            var transport = categories[1];
            var housing   = categories[2];
            var health    = categories[3];
            var entertain = categories[4];
            var shopping  = categories[5];
            var utilities = categories[6];
            var education = categories[7];

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
