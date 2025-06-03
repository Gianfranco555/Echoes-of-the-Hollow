# House Style Guide

This document outlines basic C# coding conventions for this Unity project.

## 1. Naming Conventions
- **Classes and public members**: `PascalCase`
- **Private members and local variables**: `camelCase`
- **Constants**: `SCREAMING_SNAKE_CASE`

## 2. Commenting
- Provide a brief summary comment for public classes and methods.
- Use comments to explain complex logic or non-obvious decisions.

## 3. File Organization
- Group scripts by feature or by type to keep the project organized.

## 4. Unity Specifics
- Use `[SerializeField]` on private fields that require Inspector access.
- Prefer `GetComponent<T>()` during initialization or caching references rather than repeatedly searching for components at runtime.
