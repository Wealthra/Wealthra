namespace Wealthra.Domain.Constants;

/// <summary>Canonical global categories (English + Turkish). Keep in sync with database seed and migrations.</summary>
public static class StandardGlobalCategoryPairs
{
    public static readonly (string NameEn, string NameTr)[] Pairs =
    [
        ("Food & Dining", "Yemek ve Restoran"),
        ("Transport", "Ulaşım"),
        ("Housing", "Konut"),
        ("Health & Fitness", "Sağlık ve Spor"),
        ("Entertainment", "Eğlence"),
        ("Shopping", "Alışveriş"),
        ("Utilities", "Faturalar"),
        ("Education", "Eğitim"),
        ("Groceries", "Market"),
        ("Coffee & Snacks", "Kahve ve Atıştırmalık"),
        ("Insurance", "Sigorta"),
        ("Personal Care", "Kişisel Bakım"),
        ("Gifts & Donations", "Hediye ve Bağış"),
        ("Travel & Vacation", "Seyahat ve Tatil"),
        ("Pets", "Evcil Hayvan"),
        ("Childcare & Family", "Çocuk ve Aile"),
        ("Subscriptions & Software", "Abonelik ve Yazılım"),
        ("Home Maintenance", "Ev Bakımı"),
        ("Car Maintenance", "Araç Bakımı"),
        ("Taxes & Fees", "Vergi ve Harçlar"),
        ("Business Expenses", "İş Giderleri"),
        ("Investments & Savings", "Yatırım ve Birikim"),
        ("Debt Payments", "Borç Ödemeleri"),
        ("Alcohol & Bars", "Alkol ve Bar"),
        ("Hobbies & Sports", "Hobi ve Spor"),
        ("Electronics & Gadgets", "Elektronik"),
        ("Clothing & Accessories", "Giyim ve Aksesuar"),
        ("Books & Learning", "Kitap ve Öğrenim"),
        ("Miscellaneous", "Diğer"),
        ("Cash & ATM Withdrawals", "Nakit ve ATM"),
    ];
}
