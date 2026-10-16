Description

A portable web platform: SPAs, container APIs, one PostgreSQL and S3 storage.

A monorepo with:
- static single-page web apps;
- one or more stateless APIs in containers;
- one PostgreSQL database, with one schema per API;
- S3-compatible object storage.

The only infrastructure it needs is a Postgres TCP connection, a container runtime, a static file host and an S3 endpoint. That means it runs the same way on Google Cloud, DigitalOcean, Linode, AWS or a single VM with Docker Compose. Secrets are environment variables. Identity comes from a hosted auth provider, used only behind an interface; roles and permissions live in the database. Scheduled work is plain cron in a container calling internal HTTP endpoints. There are no queues, no cloud SDKs and no vendor-only services. Consistency logic runs inside the database transaction of the write that causes it.

Where it fits:
- business apps of up to a few hundred concurrent users;
- data that is relational, with an audit trail and permissions;
- a small team that wants to move between providers or run on-premise.

Where it doesn't:
- real-time collaboration (it re-fetches after writes, there's no live sync);
- offline-first mobile;
- very high write throughput or event-driven pipelines;
- anything that truly needs a managed queue, streaming or a vendor's ML service.