# Gezinti frontend

React, TypeScript, Vite, and Leaflet client for the Gezinti location discovery app.

See the [repository README](../README.md) for the full product overview, architecture, provider setup, and end-to-end local development instructions.

## Frontend commands

```sh
npm ci
npm run dev
npm run build
```

The Vite development server defaults to `http://localhost:5173`. It connects to `http://localhost:5056` unless `VITE_API_BASE_URL` is set in a local `.env.local` file.
