namespace Wealthra.Infrastructure.Persistence.Seeding
{
    public sealed record FinancialTipDefinition(string Topic, string Body, string Locale, string Tags);

    /// <summary>
    /// Bilingual tips; embeddings are derived at seed time from Topic + Body + Tags (see FinancialTipsSeeder).
    /// </summary>
    public static class FinancialTipSeedData
    {
        public static IReadOnlyList<FinancialTipDefinition> All { get; } = BuildAll();

        private static IReadOnlyList<FinancialTipDefinition> BuildAll()
        {
            var en = new[]
            {
                new FinancialTipDefinition("Subscription audit", "List every subscription and cancel what you have not used in the last 30 days.", "en-US", "subscriptions,spending,cashflow"),
                new FinancialTipDefinition("Dining-out cap", "If dining out is a large share of income, set a weekly spending cap and track it in one category.", "en-US", "food,dining,budget"),
                new FinancialTipDefinition("Emergency fund starter", "Automate a transfer of at least 5% of income to a separate savings account each payday.", "en-US", "savings,emergency,automation"),
                new FinancialTipDefinition("Rule of 50/30/20", "Try needs under 50%, wants near 30%, and savings or debt payoff at least 20% until your buffer is healthy.", "en-US", "budget,planning,allocation"),
                new FinancialTipDefinition("Transport alternatives", "If transport spending jumps, batch errands, compare transit passes, and delay non-urgent trips.", "en-US", "transport,variable,spike"),
                new FinancialTipDefinition("Housing stress test", "If housing exceeds a third of income, list one-time cuts elsewhere before considering a move.", "en-US", "housing,income,share"),
                new FinancialTipDefinition("Debt avalanche or snowball", "Pick one method: highest interest first or smallest balance first, and pay minimums on the rest.", "en-US", "debt,interest,payoff"),
                new FinancialTipDefinition("Shopping cooldown", "For discretionary spikes, use a 48-hour rule on non-essentials over a set dollar amount.", "en-US", "shopping,impulse,discipline"),
                new FinancialTipDefinition("Utility review", "Compare providers annually and fix leaks or idle devices that inflate bills.", "en-US", "utilities,fixed,savings"),
                new FinancialTipDefinition("Health spending rhythm", "Schedule predictable costs like checkups so they do not cluster in one month.", "en-US", "health,planning,smoothing"),
                new FinancialTipDefinition("Entertainment bundle check", "Replace overlapping streaming and apps with one bundle or rotate services monthly.", "en-US", "entertainment,subscriptions"),
                new FinancialTipDefinition("Education ROI", "Before courses or certifications, define the skill or credential you need and a 90-day practice plan.", "en-US", "education,career,planning"),
                new FinancialTipDefinition("Cash envelope digital", "Use separate pots or envelopes per category so overspending is visible immediately.", "en-US", "budget,envelopes,visibility"),
                new FinancialTipDefinition("Income smoothing", "If income varies, base your budget on a three-month average and keep a small holdback fund.", "en-US", "income,irregular,buffer"),
                new FinancialTipDefinition("Month-over-month review", "When a category spikes, compare three months of totals and note one driver to fix next month.", "en-US", "trends,review,spike"),
                new FinancialTipDefinition("Credit card grace", "Pay statement balances in full when possible; track the due date to avoid interest on purchases.", "en-US", "credit,interest,timing"),
                new FinancialTipDefinition("Tax-advantaged first", "Before taxable investing, use employer match and any available tax-advantaged accounts.", "en-US", "tax,savings,longterm"),
                new FinancialTipDefinition("Gift and holiday fund", "Set aside a fixed amount monthly so seasonal spending does not blow your budget.", "en-US", "seasonal,gifts,planning"),
                new FinancialTipDefinition("Negotiate recurring bills", "Once a year, call or chat to retention offers on internet, mobile, and insurance.", "en-US", "bills,negotiation,fixed"),
                new FinancialTipDefinition("Automate minimum good habits", "Automate savings and bill pay for essentials so willpower is reserved for discretionary choices.", "en-US", "automation,habits,simplicity"),
                new FinancialTipDefinition("Track true discretionary", "Separate essential bills from flexible spending so alerts reflect choices you can change.", "en-US", "categories,awareness"),
                new FinancialTipDefinition("One financial goal", "Pick one primary goal for the quarter and align weekly spending decisions to it.", "en-US", "goals,focus,discipline"),
                new FinancialTipDefinition("Buffer before investing", "Keep one to two months of expenses in cash before increasing market risk.", "en-US", "risk,liquidity,safety"),
                new FinancialTipDefinition("Receipt habit", "Capture receipts for two weeks to catch miscategorized or duplicate charges.", "en-US", "accuracy,review,habits"),
            };

            var tr = new[]
            {
                // Topics match legacy migration rows so embeddings refresh without duplicate keys.
                new FinancialTipDefinition("Abonelik Kontrolü", "Kullanmadığın abonelikleri listele ve son 30 günde açmadıklarını iptal et.", "tr-TR", "abonelik,harcama,nakit"),
                new FinancialTipDefinition("Yeme-Disari Butcesi", "Yeme içme gelirine göre yüksekse haftalık tavan belirle ve tek kategoride izle.", "tr-TR", "yemek,restoran,butce"),
                new FinancialTipDefinition("Acil Durum Fonu", "Her maaş günü gelirin en az yüzde beşini ayrı bir tasarruf hesabına otomatik aktar.", "tr-TR", "tasarruf,acil,otomasyon"),
                new FinancialTipDefinition("50/30/20 kuralı", "İhtiyaçlar yüzde 50 altında, istekler yüzde 30 civarında, tasarruf veya borç ödemesi yüzde 20 üstü hedefle.", "tr-TR", "butce,planlama,pay"),
                new FinancialTipDefinition("Ulaşım alternatifleri", "Ulaşım harcaması arttıysa işleri topla, kartları karşılaştır ve acil olmayan yolculukları ertele.", "tr-TR", "ulasim,degisken,artis"),
                new FinancialTipDefinition("Konut stres testi", "Konut gelirin üçte birini aşıyorsa taşınmadan önce başka kalemlerde kesinti listele.", "tr-TR", "konut,gelir,pay"),
                new FinancialTipDefinition("Borç stratejisi", "Ya en yüksek faiz önce ya da en küçük bakiye önce; diğerlerinde asgari öde.", "tr-TR", "borc,faiz,odeme"),
                new FinancialTipDefinition("Alışveriş bekleme kuralı", "Harcama sıçramalarında belirlediğin tutarın üstündeki isteklere 48 saat bekle.", "tr-TR", "alisveris,dürtü,disiplin"),
                new FinancialTipDefinition("Fatura ve enerji", "Yılda bir sağlayıcıları karşılaştır; gereksiz çalışan cihazları kapat.", "tr-TR", "fatura,enerji,tasarruf"),
                new FinancialTipDefinition("Sağlık ritmi", "Kontrol ve öngörülebilir sağlık giderlerini bir aya yığmayacak şekilde yay.", "tr-TR", "saglik,planlama"),
                new FinancialTipDefinition("Eğlence paketleri", "Üst üste abonelikleri tek paket veya aylık rotasyonla sadeleştir.", "tr-TR", "eglence,abonelik"),
                new FinancialTipDefinition("Eğitim getirisi", "Kurs öncesi hedef beceriyi ve 90 günlük pratik planını yaz.", "tr-TR", "egitim,kariyer,plan"),
                new FinancialTipDefinition("Dijital zarf", "Kategori başına ayrı kova kullan; taşmayı hemen gör.", "tr-TR", "butce,zarf,farkındalık"),
                new FinancialTipDefinition("Düzensiz gelir", "Bütçeyi üç aylık ortalamaya göre kur; küçük bir yedek tut.", "tr-TR", "gelir,dalgalanma,tampon"),
                new FinancialTipDefinition("Ay içi artış analizi", "Bir kategori sıçradıysa üç ayın toplamını karşılaştır; bir nedeni düzelt.", "tr-TR", "trend,inceleme,artis"),
                new FinancialTipDefinition("Kredi kartı vadesi", "Mümkünse ekstre bakiyesini tam kapat; faizden kaçınmak için son ödeme gününü takip et.", "tr-TR", "kredi,faiz,zamanlama"),
                new FinancialTipDefinition("Önce vergi avantajı", "Yatırımdan önce işveren eşleşmesi ve uygun vergi avantajlı hesapları değerlendir.", "tr-TR", "vergi,tasarruf,uzunvadeli"),
                new FinancialTipDefinition("Hediye ve bayram fonu", "Sezonluk harcamalar için aya sabit ayır; bütçeyi boğmaz.", "tr-TR", "hediye,sezon,plan"),
                new FinancialTipDefinition("Sabit gider pazarlığı", "İnternet, mobil ve sigortada yılda bir kez yenileme veya kampanya iste.", "tr-TR", "fatura,pazarlik,sabit"),
                new FinancialTipDefinition("İyi alışkanlıkları otomatikleştir", "Tasarruf ve zorunlu ödemeleri otomatikleştir; iradeyi esnek harcamaya bırak.", "tr-TR", "otomasyon,aliskanlik"),
                new FinancialTipDefinition("Esnek harcamayı ayır", "Zorunlu faturaları esnek harcamadan ayır; uyarılar gerçekten değiştirebileceğin kalemleri göstersin.", "tr-TR", "kategori,farkındalık"),
                new FinancialTipDefinition("Tek finansal hedef", "Çeyrek için bir ana hedef seç; haftalık harcamaları ona göre hizala.", "tr-TR", "hedef,odak,disiplin"),
                new FinancialTipDefinition("Tampon sonra risk", "Piyasaya risk artırmadan önce bir iki aylık gideri nakit tamponda tut.", "tr-TR", "risk,likidite,güvenlik"),
                new FinancialTipDefinition("Fiş alışkanlığı", "İki hafta fiş topla; yanlış veya mükerrer kayıtları yakala.", "tr-TR", "dogruluk,inceleme,aliskanlik"),
            };

            return en.Concat(tr).ToList();
        }
    }
}
