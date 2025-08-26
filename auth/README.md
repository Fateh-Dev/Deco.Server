# Auth Module (Backend)

## Setup
1. Add to `appsettings.json`:
   ```json
   "Jwt": {
     "Key": "your_secret_key_here"
   }
   ```
2. Run EF Core migration to add password fields to User table.

## Endpoints
- `POST /api/auth/register` — Register new user
- `POST /api/auth/login` — Login and get JWT

## Notes
- Passwords are hashed and salted.
- Uses JWT for authentication.
