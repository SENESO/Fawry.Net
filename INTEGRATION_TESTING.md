# Integration Testing

The `test/Fawry.IntegrationTests` project exercises the SDK against Fawry's
**staging (sandbox) environment** — the SDK default, `https://atfawry.fawrystaging.com`.
It never touches production and never moves money.

## Environment variables

| Variable             | Required | Description                                                |
|----------------------|----------|------------------------------------------------------------|
| `FAWRY_MERCHANT_CODE` | yes      | Merchant code from your Fawry sandbox account              |
| `FAWRY_SECURITY_KEY`  | yes      | Secure key from your Fawry sandbox account                 |
| `FAWRY_BASE_URL`      | no       | Override for the API base URL; defaults to staging (`https://atfawry.fawrystaging.com`) |

- Every test skips (`Assert.Ignore`) when the required variables are absent, so
  the suite stays green on machines without sandbox access — including CI.
- Tests also skip gracefully if staging is unreachable from the machine.
- Never commit real credentials. Never use production keys here.

## Where to get sandbox credentials

1. Go through Fawry merchant onboarding for a test account. The FawryPay team
   issues a **merchant code** and a **secure key** for the staging environment
   during account setup.
2. API documentation and the signature recipes live on the developer portal:
   **https://developer.fawrystaging.com**.
3. The staging console/API is at **https://atfawry.fawrystaging.com**
   (production is `https://www.atfawry.com`).

## How to run

```bash
export FAWRY_MERCHANT_CODE="your-sandbox-merchant-code"
export FAWRY_SECURITY_KEY="your-sandbox-secure-key"
# optional: export FAWRY_BASE_URL="https://atfawry.fawrystaging.com"

dotnet test test/Fawry.IntegrationTests -c Release
```

## What the tests cover

All tests are read-oriented or exercise documented error paths with
probe references (`integration-probe-<guid>`) that cannot match real orders:

1. `Staging_IsReachable_AndReturnsFawryEnvelope` — a signed charge-status
   query returns a parseable Fawry JSON envelope (connectivity + TLS + URL
   construction against staging).
2. `ChargeStatusQuery_SignatureRecipe_RoundTripsAgainstStaging` — status
   query signed with the SDK's recipe (`merchantCode + merchantRefNumber +
   secureKey`); with live sandbox credentials it also asserts a real
   `orderStatus` and verifies staging's response signature via
   `FawryClient.VerifyCallbackSignature`.
3. `TamperedSignature_StatusQuery_IsRejectedByStaging` — a corrupted
   signature must be rejected with Fawry's documented invalid-signature
   status code `9946`.
4. `Capture_NonExistentAuthorization_ThrowsFawryApiException` — the SDK's
   capture plumbing against staging; the dummy reference is rejected
   gracefully as a `FawryApiException` carrying Fawry's own status code.
5. `CancelAuthorization_NonExistentReference_ThrowsFawryApiException` —
   the same graceful-rejection proof through the cancel path.

No test initiates a payment, captures funds, cancels a real authorization,
or moves money in any way.
