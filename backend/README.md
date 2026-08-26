# Grimhold Backend

Node.js + Express + Mongoose backend para Project Grimhold.

## Requisitos

- Node.js 20+
- MongoDB Atlas o instancia local

## Setup local

1. Copiar .env.example a .env y completar los valores.
2. 
pm install
3. 
pm run dev

## Variables de entorno

| Variable        | Requerida | Descripción                                           |
|-----------------|-----------|-------------------------------------------------------|
| PORT          | Sí        | Puerto del servidor                                   |
| MONGODB_URI   | Sí        | Connection string de MongoDB                          |
| JWT_SECRET    | Sí        | Secreto HMAC-SHA256 para firmar JWT                   |
| JWT_EXPIRES_IN| No        | Duración del token en segundos (default: 3600)        |

## Endpoints disponibles

- GET /health — Estado del servidor

## Atlas Network Access

Si se usa MongoDB Atlas, agregar la IP del equipo en **Network Access** antes de iniciar el servidor.
