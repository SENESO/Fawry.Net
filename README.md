![Fawry.Net](docs/banner.png)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net8.0-512BD4)](https://dotnet.microsoft.com/)
[![GitHub stars](https://img.shields.io/github/stars/SENESO/Fawry.Net)](https://github.com/SENESO/Fawry.Net/stargazers)
[![NuGet](https://img.shields.io/nuget/v/Fawry.Net)](https://www.nuget.org/packages/Fawry.Net)
[![CI](https://github.com/SENESO/Fawry.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/SENESO/Fawry.Net/actions/workflows/ci.yml)

# Fawry.Net

The missing .NET SDK for **Fawry** — Egypt's largest payment network.

Create Pay-at-Fawry reference numbers (cash at 300k+ outlets), charge cards
(immediate or authorize-then-capture), take mobile-wallet payments, capture and
cancel authorizations, issue refunds, and verify payment callbacks. Request signing follows
Fawry's official documentation exactly — you never hand-roll a signature again.

Targets `netstandard2.0` and `net8.0`. Only `System.Text.Json` (+ DI abstractions).
Bring your own `HttpClient`.

## Install

```bash
dotnet add package Fawry.Net
```

Or clone and reference the project:

```bash
git clone https://github.com/SENESO/Fawry.Net.git
```

## Quickstart

```csharp
using Fawry;
using Fawry.Models;

var client = new FawryClient(new FawryClientOptions
{
    MerchantCode = "<your-merchant-code>",
    SecureKey = "<your-secure-key>",
    // BaseUrl defaults to staging. Go live with:
    // BaseUrl = FawryClientOptions.ProductionBaseUrl
});

// Pay-at-Fawry: customer pays cash at any outlet with the reference number
var charge = await client.ChargePayAtFawryAsync(new ChargeRequest
{
    MerchantRefNum = Guid.NewGuid().ToString("N"), // unique per charge
    CustomerName = "Ahmed Ali",
    CustomerMobile = "01234567891",
    CustomerEmail = "ahmed@example.com",
    Amount = 580.55m,
    ChargeItems =
    {
        new ChargeItem { ItemId = "sku-1", Description = "Blue T-shirt", Price = 580.55m, Quantity = 1 }
    }
});

Console.WriteLine(charge.ReferenceNumber); // <-- customer pays with this
```

## API reference

### Charges

```csharp
// Pay-at-Fawry (cash at outlets)
Task<ChargeResponse> ChargePayAtFawryAsync(ChargeRequest request, CancellationToken ct = default)

// Mobile wallet (e.g. Vodafone Cash) — customer approves on their phone
Task<ChargeResponse> ChargeMobileWalletAsync(ChargeRequest request, CancellationToken ct = default)

// Card payment. Set AuthCaptureMode = true to authorize now and capture later.
Task<ChargeResponse> ChargeCardAsync(CardChargeRequest request, CancellationToken ct = default)
```

Card example (authorize now, capture on shipment):

```csharp
var auth = await client.ChargeCardAsync(new CardChargeRequest
{
    MerchantRefNum = Guid.NewGuid().ToString("N"),
    CustomerName = "Ahmed Ali",
    CustomerMobile = "01234567891",
    CustomerEmail = "ahmed@example.com",
    Amount = 1200.00m,
    CardNumber = "4242424242424242",
    CardExpiryYear = "27",
    CardExpiryMonth = "05",
    Cvv = "123",
    AuthCaptureMode = true, // hold the funds, capture later
    ChargeItems = { new ChargeItem { ItemId = "sku-2", Description = "Laptop", Price = 1200.00m, Quantity = 1 } }
});

// ... when you ship:
var capture = await client.CaptureAsync(auth.MerchantRefNumber); // full amount
var partial = await client.CaptureAsync(auth.MerchantRefNumber, 800.00m); // partial

// or release the hold:
var cancel = await client.CancelAuthorizationAsync(auth.MerchantRefNumber);
```

### Refunds

```csharp
// Refund a captured payment — full or partial. referenceNumber is Fawry's
// order reference from the charge response.
var refund = await client.RefundAsync(charge.ReferenceNumber, 580.55m, "Item not as described");

// partial refund, no reason:
var partial = await client.RefundAsync(charge.ReferenceNumber, 200.00m);

// FawryApiException on failure, e.g. "9935" = refund exceeds paid amount,
// "9954" = order not paid, "9938" = order not found.
```

Signature: `Task<RefundResponse> RefundAsync(string referenceNumber, decimal refundAmount, string reason = null, CancellationToken ct = default)`

### Callbacks — always verify

```csharp
[HttpPost("api/fawry/callback")]
public IActionResult Callback([FromBody] ChargeResponse callback)
{
    if (!client.VerifyCallbackSignature(callback))
        return Unauthorized(); // constant-time SHA-256 comparison

    if (callback.IsPaid)
    {
        // fulfill order callback.MerchantRefNumber ...
    }
    return Ok();
}
```

Never trust callback query params alone — verify the signature first.

Server-to-server notifications V2 send the signature as `messageSignature`
(computed with Fawry's V2 recipe) — the SDK detects it automatically and
verifies with the V2 recipe; legacy `signature` payloads keep the legacy recipe.

### ASP.NET Core DI

```csharp
services.AddFawry(options =>
{
    options.MerchantCode = builder.Configuration["Fawry:MerchantCode"];
    options.SecureKey = builder.Configuration["Fawry:SecureKey"];
    options.BaseUrl = FawryClientOptions.ProductionBaseUrl;
});

// then inject FawryClient anywhere
```

### Errors

Non-200 responses throw `FawryApiException` with `StatusCode`
(e.g. `"9946"` = blank or invalid signature), `StatusDescription`,
and the raw `ResponseBody`.

## Staging vs production

| Environment | Base URL |
|---|---|
| Staging (default) | `https://atfawry.fawrystaging.com` |
| Production | `https://www.atfawry.com` |

Set `FawryClientOptions.BaseUrl` accordingly. Test credentials come from the
FawryPay team during account setup.

## Notes & honesty

- Signature recipes were taken from Fawry's official docs
  ([developer.fawrystaging.com](https://developer.fawrystaging.com), "Authorize and
  Capture Payments" + charge API pages, verified Oct 2026). Card charges use the
  card-inclusive recipe from those docs.
- Refund signature: `merchantCode + referenceNumber + refundAmount (2dp) + reason (or "") + secureKey`,
  per Fawry's official "Refund API" page
  ([refund-issue-api](https://developer.fawrystaging.com/docs/server-apis/refund-issue-api)),
  verified Oct 2026.
- Callback verification follows Fawry's "Server To Server Notification V2" page:
  `fawryRefNumber + merchantRefNumber + paymentAmount (2dp) + orderAmount (2dp) +
  orderStatus + paymentMethod + paymentRefrenceNumber (or "") + secureKey`
  (field names as documented, typo included). The previous implementation used a
  legacy recipe with extra fields — V2 payloads are now verified correctly.
- Fawry sends `statusCode` as a JSON number on some endpoints and a string on
  others — the SDK reads both into the string `StatusCode` property.
- Amounts are sent as two-decimal strings (`"580.55"`), matching Fawry's own
  examples, so the signed value is always exactly what is sent.
- `ChargeResponse.ShippingFees` is included for callback verification but is
  rarely populated by Fawry.
- `FawryPaymentMethods.Valu` is defined for completeness; dedicated ValU flows
  are not wrapped yet (the charge recipe is identical — open an issue if you need it).

## License

MIT — see [LICENSE](LICENSE).
