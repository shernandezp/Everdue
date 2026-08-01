# Security policy

## Reporting a vulnerability

**Please do not open a public issue.**

Report it privately through GitHub's [private vulnerability reporting](https://docs.github.com/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
on this repository (Security → Report a vulnerability), or by e-mail to the address in the repository's profile.

Please include what you did, what happened, and what you expected. A proof of concept helps; a full exploit is not
required.

### What to expect

Everdue is maintained by a very small team, so here is the honest commitment rather than a corporate one:

- **Acknowledgement within 5 working days.**
- An assessment and a plan within **15 working days** of acknowledgement.
- A fix released as fast as severity warrants; a self-hosted product means the fix only helps once people upgrade,
  so releases carry a clear note when one is security-relevant.
- Credit in the release notes if you want it.

There is no bug bounty.

## Supported versions

The latest release only. Everdue is self-hosted and there is no backport branch.

## Scope

In scope: anything in this repository — the API, the SPA, the packaging in `deploy/`, the default configuration.

Particularly interesting:

- **Cross-tenant leakage.** Every tenant-owned table sits behind one EF Core global query filter. A query that
  escapes it is the most serious class of bug in this codebase.
- **API keys.** A key must not be usable outside its endpoint allow-list or its read/write scope, whatever role
  its actor holds.
- **Webhooks.** Signing, and the fact that a subscriber's response is never treated as input.
- **Attachment and import upload paths**, including the storage key never deriving from a user-supplied filename.
- Authentication, the forced-password-change gate, and the cookie posture (`SameSite=Strict`, no CORS).

## Known and accepted limitations

These are documented decisions, not undiscovered bugs. A report about one of them is welcome as a discussion, but
it is not a vulnerability report:

- **No virus scanning or content sniffing on attachments.** The posture is an extension and content-type
  allow-list, authenticated download, `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`.
  Scanning needs a daemon, which is the opposite of the one-binary install promise.
- **Webhooks may target private and loopback addresses.** Posting to an automation box on the same LAN is the
  actual self-hosted use case, only an administrator can create a subscription, and that administrator can already
  read everything the payload would carry. Redirects are not followed and response bodies are discarded.
- **SQLite and the data-protection key ring are unencrypted at rest.** Protection is the volume's file
  permissions; use OS or disk-level encryption if you need more.
- **`Security:RequireHttps` is off by default**, because self-hosters routinely terminate TLS at their own reverse
  proxy and a cookie the browser refuses to send looks exactly like a broken password. Turn it on for any install
  that terminates TLS at the app.
