---
title: Verification API
aliases:
  - Verification Endpoint
  - isVerified API
tags:
  - api
  - verification
---

# Verification API

The verification API checks playbook trustworthiness.

## Endpoint

```
GET http://{server}/isVerified
```

## Servers

| Region | URL |
|--------|-----|
| Europe | `wng-eu.ktwirzade.com:8000` |
| Americas/Asia-Pacific | `wng-us.ktwirzade.com:8000` |

## Request

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `prodID` | string | Yes | Product code from playbook |
| `hash` | string | Yes | SHA256 hash of .apbx file |

### Example Request

```
GET http://wng-eu.ktwirzade.com:8000/isVerified?prodID=ABC123&hash=a1b2c3d4...
```

## Response

### Format

```json
{"isVerified": "true"}
```

### Values

| Value | Meaning |
|-------|---------|
| `"true"` | Playbook is verified |
| `"false"` | Playbook is not verified |
| `"malicious"` | Playbook is malicious |

## Client Implementation

### Hash Calculation

```csharp
string CalculateSHA256(string filePath)
{
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = sha256.ComputeHash(stream);
    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
}
```

### Verification Check

```csharp
async Task<string> CheckVerification(string productCode, string hash)
{
    using var client = new HttpClient();
    var url = $"http://wng-eu.ktwirzade.com:8000/isVerified?prodID={productCode}&hash={hash}";
    var response = await client.GetStringAsync(url);
    var json = JObject.Parse(response);
    return json["isVerified"]?.ToString();
}
```

## Status Storage

Verification results are stored locally:

```
%PROGRAMDATA%\KTWirzade\Playbooks\{GUID}.status
```

## Error Handling

| Scenario | Client Behavior |
|----------|-----------------|
| Server unreachable | Status = `Unreached` |
| Invalid response | Status = `Unverified` |
| Network timeout | Status = `Unreached` |
| No ProductCode | Status = `Unverified` |

## Security

- API uses HTTP (not HTTPS) for internal communication
- No authentication required
- Hash prevents forged verification requests

---

> [!info] See Also
> - [[Playbooks/Verification-System]] - Verification system overview
> - [[API/Update-API]] - Update API
