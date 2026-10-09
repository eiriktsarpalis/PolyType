using PolyType;
using System.Globalization;

// A connected commerce/service model with varied constructor signatures.
// Both consumers traverse this entire graph, including every union case.
[GenerateShape]
internal partial record RepresentativeModel(
    TenantIdentifier Tenant,
    Customer Customer,
    Organization Organization,
    ProductCategory Category,
    CatalogSettings Catalog,
    Order Order,
    Payment[] Payments,
    BillingAccount Billing,
    ShippingPlan Shipping,
    InventoryItem Inventory,
    Supplier Supplier,
    IdentitySettings Identity,
    UserAccount User,
    AuditEntry Audit,
    Notification[] Notifications,
    SupportTicket Support,
    MetricSeries Metrics,
    AnalyticsSettings Analytics,
    FeatureFlag Feature,
    ServiceSettings Service)
{
    public static RepresentativeModel CreateSample()
    {
        Guid id = new("12345678-1234-5678-9abc-123456789abc");
        DateTimeOffset timestamp = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var address = new PostalAddress("Example Street", "Oslo", "0123", "NO", 59.91, 10.75);
        var contact = new ContactDetails("customer@example.test", "+4700000000", address, ["email", "sms"]);
        var profile = new CustomerProfile("Ada", "Lovelace", new DateOnly(1990, 1, 1), "en", new() { ["newsletter"] = true });
        var customer = new Customer(id, profile, contact,
            new LoyaltyAccount(7, 1200L, 0.15m, new DateOnly(2025, 1, 1)),
            new ConsentPreferences(true, false, timestamp, 2),
            new CustomerSegment("regular", 0.8, 50, ["returning"]));
        var organization = new Organization(42L, "Example", new Uri("https://example.test"), address,
            new OrganizationContact("Operations", contact, true), ["NO", "SE"]);

        var description = new ProductDescription("Notebook", "A durable notebook.", new() { ["en"] = "Notebook" }, ["paper", "office"]);
        var dimensions = new ProductDimensions(0.2, 0.15, 0.01, 0.3f);
        var price = new ProductPrice(19.95m, "NOK", 0.25m, null);
        var variant = new ProductVariant("NOTE-BLUE", "Blue", dimensions, price, new() { ["color"] = "blue" });
        var product = new Product(id, description, dimensions, [variant],
            [new ProductReview(5, "Useful", customer, timestamp, true)],
            [new ProductImage(new Uri("https://example.test/notebook.png"), "Notebook", 640, 480)],
            new ProductAvailability(true, 100, null, ["online"]), timestamp);
        var category = new ProductCategory(12, "Stationery", [product], new() { ["featured"] = product });
        var catalog = new CatalogSettings("en", "NOK", 24, true, new() { ["NO"] = 0.25m }, ["paper"]);

        var taxes = new TaxBreakdown("VAT", 0.25m, 4m, new() { ["standard"] = 4m });
        var coupon = new Coupon("WELCOME", 0.05m, timestamp.AddDays(30), 100, true);
        var line = new OrderLine(1, product, variant, 2, price, ["gift"]);
        var order = new Order(id, customer, [line],
            new OrderTotals(39.90m, 4m, 5m, 48.90m, "NOK"),
            [new OrderNote("Leave at reception", timestamp, true)],
            [new OrderStatusHistory(OrderStatus.Confirmed, timestamp, "system")],
            new PurchaseApproval(true, 7L, timestamp, "Approved"), coupon, taxes, timestamp);

        var authorization = new PaymentAuthorization("AUTH-1", true, timestamp, 48.90m, new() { ["risk"] = "low" });
        Payment[] payments =
        [
            new Payment(id, order.Id, new CardPayment("VISA", "4242", 12, 2030, true), authorization,
                new Refund("REF-1", 1.25m, timestamp, "Adjustment"), 48.90m, PaymentStatus.Captured),
            new Payment(id, order.Id, new BankTransferPayment("NO0000000000", "EXAMPLE", "ORDER-1"), authorization,
                null, 48.90m, PaymentStatus.Pending),
            new Payment(id, order.Id, new WalletPayment("example-wallet", "ada", 0.75m, true), authorization,
                null, 48.90m, PaymentStatus.Authorized),
        ];
        var billing = new BillingAccount(9L, customer, address,
            new BillingPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 31),
            500m, "NOK", ["monthly"]);

        var carrier = new Carrier("EXAMPLE", "Example Carrier", new Uri("https://example.test/tracking"), ["NO", "SE"], true);
        var center = new FulfillmentCenter(3, "Oslo", address, new TimeOnly(8, 0), new TimeOnly(17, 0), 500);
        var window = new DeliveryWindow(timestamp.AddDays(1), timestamp.AddDays(2), true, "+4700000000");
        var package = new Package("PKG-1", dimensions, 0.6, false, [line]);
        var shipment = new Shipment(id, order.Id, carrier, [package],
            [new TrackingEvent("Dispatched", timestamp, address, 1)], window, ShipmentStatus.Dispatched);
        var shipping = new ShippingPlan("standard", carrier, center, [shipment], window, 5m);

        var stock = new StockLevel(center, 100, 2, timestamp);
        var reservation = new StockReservation(id, order.Id, 2, timestamp.AddHours(2), false);
        var inventory = new InventoryItem(product, [stock], [reservation],
            new RestockPolicy(10, 100, TimeSpan.FromDays(3), true), new() { ["Oslo"] = 100 });
        var supplier = new Supplier(15L, "Paper supplier", address,
            new SupplierContact("Sales", "supplier@example.test", "+4700000001", true),
            [product], new() { ["quality"] = 0.95 });

        var permission = new Permission("orders", "read", true, ["own"]);
        var role = new Role(1, "customer", [permission], true);
        var credential = new UserCredential("password", "not-a-secret", timestamp, true);
        var session = new Session(id, timestamp, timestamp.AddHours(1), "127.0.0.1", true);
        var user = new UserAccount(id, "ada", contact, [credential], [role], [session], false);
        var identity = new IdentitySettings("example", TimeSpan.FromHours(1), true, 8, new() { ["customer"] = role }, new HashSet<Guid> { id });
        var audit = new AuditEntry(42L, user, "order-created", timestamp, order.Id, new() { ["channel"] = "web" });
        Notification[] notifications =
        [
            new EmailNotification(id, contact.Email, "Order confirmation", "Confirmed", false),
            new SmsNotification(id, contact.Phone, "Your order is confirmed", 1),
            new WebhookNotification(id, new Uri("https://example.test/hooks"), new() { ["Accept"] = "application/json" }, 3, true),
        ];

        var support = new SupportTicket(101L, customer, "Delivery question",
            [new SupportComment(user, "When will it arrive?", timestamp, false)],
            [new SupportAttachment("receipt.pdf", new Uri("https://example.test/receipt.pdf"), 2048L, "application/pdf")],
            new SupportSla(TimeSpan.FromHours(4), TimeSpan.FromDays(1), 2, true), TicketStatus.Open, timestamp);
        var metrics = new MetricSeries("orders", [new MetricPoint(timestamp, 12.5, 10L, ["web"])],
            new() { ["region"] = "NO" }, TimeSpan.FromMinutes(1));
        var analytics = new AnalyticsSettings(true, 0.5, TimeSpan.FromDays(30), ["orders"], new() { ["revenue"] = 100m });
        var rule = new FeatureRule("country", RuleOperator.Equal, "NO", 10, false);
        var feature = new FeatureFlag("new-checkout", true, 25, [rule], new() { ["NO"] = true });
        var retry = new RetryPolicy(3, TimeSpan.FromSeconds(1), 2.0, true);
        var cache = new CachePolicy(TimeSpan.FromMinutes(5), 1024L, true, ["catalog"]);
        var endpoint = new EndpointSettings(new Uri("https://example.test/api"), TimeSpan.FromSeconds(30), retry,
            new() { ["Accept"] = "application/json" }, true);
        var service = new ServiceSettings("commerce", 8080, new Version(1, 2), endpoint, retry, cache,
            new() { ["api"] = endpoint }, [feature], true);

        return new RepresentativeModel(new TenantIdentifier("tenant", 42), customer, organization, category, catalog,
            order, payments, billing, shipping, inventory, supplier, identity, user, audit, notifications,
            support, metrics, analytics, feature, service);
    }
}

internal sealed record Customer(Guid Id, CustomerProfile Profile, ContactDetails Contact, LoyaltyAccount Loyalty, ConsentPreferences Consent, CustomerSegment Segment);
internal sealed record CustomerProfile(string FirstName, string LastName, DateOnly Birthday, string Locale, Dictionary<string, bool> Preferences);
internal sealed record ContactDetails(string Email, string Phone, PostalAddress Address, string[] Channels);
internal sealed record PostalAddress(string Street, string City, string PostalCode, string Country, double Latitude, double Longitude);
internal sealed record LoyaltyAccount(int Tier, long Points, decimal Discount, DateOnly Joined);
internal sealed record ConsentPreferences(bool Email, bool Sms, DateTimeOffset Updated, ushort Version);
internal sealed record CustomerSegment(string Name, double Score, int MinimumOrders, string[] Labels);
internal sealed record Organization(long Id, string Name, Uri Website, PostalAddress Address, OrganizationContact Contact, string[] Countries);
internal sealed record OrganizationContact(string Department, ContactDetails Contact, bool Primary);

internal sealed record Product(Guid Id, ProductDescription Description, ProductDimensions Dimensions, ProductVariant[] Variants, ProductReview[] Reviews, ProductImage[] Images, ProductAvailability Availability, DateTimeOffset Updated);
internal sealed record ProductDescription(string Name, string Description, Dictionary<string, string> Translations, HashSet<string> Tags);
internal sealed record ProductDimensions(double Length, double Width, double Height, float Weight);
internal sealed record ProductVariant(string Sku, string Name, ProductDimensions Dimensions, ProductPrice Price, Dictionary<string, string> Attributes);
internal sealed record ProductPrice(decimal Amount, string Currency, decimal TaxRate, decimal? Discount);
internal sealed record ProductReview(byte Stars, string Text, Customer Author, DateTimeOffset Created, bool Verified);
internal sealed record ProductImage(Uri Url, string AltText, int Width, int Height);
internal sealed record ProductAvailability(bool InStock, int Quantity, DateOnly? RestockDate, string[] Channels);
internal sealed record ProductCategory(int Id, string Name, Product[] Products, Dictionary<string, Product> Featured);
internal sealed record CatalogSettings(string Locale, string Currency, int PageSize, bool ShowOutOfStock, Dictionary<string, decimal> TaxRates, string[] Tags);

internal sealed record Order(Guid Id, Customer Customer, OrderLine[] Lines, OrderTotals Totals, OrderNote[] Notes, OrderStatusHistory[] History, PurchaseApproval Approval, Coupon Coupon, TaxBreakdown Tax, DateTimeOffset Created);
internal sealed record OrderLine(int Position, Product Product, ProductVariant Variant, int Quantity, ProductPrice Price, string[] Options);
internal sealed record OrderTotals(decimal Subtotal, decimal Tax, decimal Shipping, decimal Total, string Currency);
internal sealed record OrderNote(string Text, DateTimeOffset Created, bool Internal);
internal sealed record OrderStatusHistory(OrderStatus Status, DateTimeOffset Changed, string Actor);
internal sealed record PurchaseApproval(bool Approved, long ApproverId, DateTimeOffset At, string Reason);
internal sealed record Coupon(string Code, decimal Discount, DateTimeOffset Expires, int RemainingUses, bool Stackable);
internal sealed record TaxBreakdown(string Name, decimal Rate, decimal Amount, Dictionary<string, decimal> Components);

internal sealed record Payment(Guid Id, Guid OrderId, PaymentMethod Method, PaymentAuthorization Authorization, Refund? Refund, decimal Amount, PaymentStatus Status);
[DerivedTypeShape(typeof(CardPayment))]
[DerivedTypeShape(typeof(BankTransferPayment))]
[DerivedTypeShape(typeof(WalletPayment))]
internal abstract record PaymentMethod;
internal sealed record CardPayment(string Network, string LastDigits, byte ExpiryMonth, ushort ExpiryYear, bool Verified) : PaymentMethod;
internal sealed record BankTransferPayment(string Account, string Bank, string Reference) : PaymentMethod;
internal sealed record WalletPayment(string Provider, string Account, decimal Credit, bool Verified) : PaymentMethod;
internal sealed record PaymentAuthorization(string Code, bool Approved, DateTimeOffset At, decimal Amount, Dictionary<string, string> Metadata);
internal sealed record Refund(string Reference, decimal Amount, DateTimeOffset At, string Reason);
internal sealed record BillingAccount(long Id, Customer Customer, PostalAddress Address, BillingPeriod Period, decimal Limit, string Currency, string[] Tags);
internal sealed record BillingPeriod(DateOnly Start, DateOnly End, short Days);

internal sealed record ShippingPlan(string Name, Carrier Carrier, FulfillmentCenter Center, Shipment[] Shipments, DeliveryWindow Window, decimal Cost);
internal sealed record Shipment(Guid Id, Guid OrderId, Carrier Carrier, Package[] Packages, TrackingEvent[] Events, DeliveryWindow Window, ShipmentStatus Status);
internal sealed record Package(string Code, ProductDimensions Dimensions, double Weight, bool Fragile, OrderLine[] Contents);
internal sealed record TrackingEvent(string Description, DateTimeOffset At, PostalAddress Location, int Sequence);
internal sealed record DeliveryWindow(DateTimeOffset From, DateTimeOffset To, bool SignatureRequired, string Phone);
internal sealed record Carrier(string Code, string Name, Uri TrackingUrl, string[] Countries, bool Active);
internal sealed record FulfillmentCenter(int Id, string Name, PostalAddress Address, TimeOnly Opens, TimeOnly Closes, int Capacity);
internal sealed record InventoryItem(Product Product, StockLevel[] Stock, StockReservation[] Reservations, RestockPolicy Restock, Dictionary<string, int> WarehouseCounts);
internal sealed record StockLevel(FulfillmentCenter Center, int Available, int Reserved, DateTimeOffset Updated);
internal sealed record StockReservation(Guid Id, Guid OrderId, int Quantity, DateTimeOffset Expires, bool Confirmed);
internal sealed record RestockPolicy(int Minimum, int Target, TimeSpan LeadTime, bool Automatic);
internal sealed record Supplier(long Id, string Name, PostalAddress Address, SupplierContact Contact, Product[] Products, Dictionary<string, double> Scores);
internal sealed record SupplierContact(string Name, string Email, string Phone, bool Primary);

internal sealed record IdentitySettings(string Issuer, TimeSpan SessionLifetime, bool RequireMfa, byte MinimumPasswordLength, Dictionary<string, Role> Roles, HashSet<Guid> TrustedDevices);
internal sealed record UserAccount(Guid Id, string Name, ContactDetails Contact, UserCredential[] Credentials, Role[] Roles, Session[] Sessions, bool Disabled);
internal sealed record UserCredential(string Kind, string Description, DateTimeOffset Changed, bool Verified);
internal sealed record Role(int Id, string Name, Permission[] Permissions, bool Enabled);
internal sealed record Permission(string Resource, string Action, bool Allowed, string[] Scopes);
internal sealed record Session(Guid Id, DateTimeOffset Created, DateTimeOffset Expires, string Address, bool Trusted);
internal sealed record AuditEntry(long Sequence, UserAccount Actor, string Action, DateTimeOffset At, Guid CorrelationId, Dictionary<string, string> Details);
[DerivedTypeShape(typeof(EmailNotification))]
[DerivedTypeShape(typeof(SmsNotification))]
[DerivedTypeShape(typeof(WebhookNotification))]
internal abstract record Notification(Guid Id);
internal sealed record EmailNotification(Guid Id, string Address, string Subject, string Body, bool Html) : Notification(Id);
internal sealed record SmsNotification(Guid Id, string Number, string Text, byte Priority) : Notification(Id);
internal sealed record WebhookNotification(Guid Id, Uri Url, Dictionary<string, string> Headers, int Retries, bool Signed) : Notification(Id);

internal sealed record SupportTicket(long Id, Customer Customer, string Subject, SupportComment[] Comments, SupportAttachment[] Attachments, SupportSla Sla, TicketStatus Status, DateTimeOffset Created);
internal sealed record SupportComment(UserAccount Author, string Text, DateTimeOffset Created, bool Internal);
internal sealed record SupportAttachment(string Name, Uri Url, long Length, string ContentType);
internal sealed record SupportSla(TimeSpan ResponseTime, TimeSpan ResolutionTime, byte Severity, bool BusinessHoursOnly);
internal sealed record MetricPoint(DateTimeOffset At, double Value, long Count, string[] Tags);
internal sealed record MetricSeries(string Name, MetricPoint[] Points, Dictionary<string, string> Dimensions, TimeSpan Interval);
internal sealed record AnalyticsSettings(bool Enabled, double SamplingRate, TimeSpan Retention, string[] Events, Dictionary<string, decimal> Targets);
internal sealed record FeatureFlag(string Name, bool Enabled, byte Rollout, FeatureRule[] Rules, Dictionary<string, bool> Regions);
internal sealed record FeatureRule(string Property, RuleOperator Operator, string Value, int Priority, bool Negate);
internal sealed record ServiceSettings(string Name, ushort Port, Version Version, EndpointSettings Endpoint, RetryPolicy Retry, CachePolicy Cache, Dictionary<string, EndpointSettings> Endpoints, FeatureFlag[] Features, bool Healthy = true);
internal sealed record EndpointSettings(Uri Url, TimeSpan Timeout, RetryPolicy Retry, Dictionary<string, string> Headers, bool Enabled);
internal sealed record RetryPolicy(byte Attempts, TimeSpan Delay, double Multiplier, bool Jitter = false);
internal sealed record CachePolicy(TimeSpan Lifetime, long Capacity, bool Sliding, string[] Tags);

[TypeShape(Marshaler = typeof(Marshaler))]
internal sealed record TenantIdentifier(string Scheme, int Value)
{
    public sealed class Marshaler : IMarshaler<TenantIdentifier, string>
    {
        public string? Marshal(TenantIdentifier? value)
            => value is null ? null : $"{value.Scheme}:{value.Value.ToString(CultureInfo.InvariantCulture)}";

        public TenantIdentifier? Unmarshal(string? value)
        {
            if (value is null)
            {
                return null;
            }

            int separator = value.IndexOf(':');
            return new TenantIdentifier(value[..separator], int.Parse(value[(separator + 1)..], CultureInfo.InvariantCulture));
        }
    }
}

internal enum OrderStatus { Pending, Confirmed, Shipped, Complete }
internal enum PaymentStatus { Pending, Authorized, Captured, Refunded }
internal enum ShipmentStatus { Pending, Dispatched, Delivered }
internal enum TicketStatus { Open, Assigned, Closed }
internal enum RuleOperator { Equal, Contains, GreaterThan }
