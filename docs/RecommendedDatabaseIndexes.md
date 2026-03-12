# Recommended Database Indexes for GET APIs (Peak-Hour Optimization)

Based on analysis of all GET endpoints, joins, and filters in the API, the following indexes are recommended. Apply these on your SQL Server database. **No application code changes required.**

---

## 1. **Users** (DataAccessScopeHelper.ResolveAsync – every request)

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_Users_Username` | `(Username)` | Lookup by username when userId not in token. |
| `IX_Users_LevelId` | `(LevelId)` | Role resolution when role name not in claims. |

*PK on `Id` already exists; used for lookup by Id.*

---

## 2. **Roles**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_Roles_Id_IsDeleted` | `(Id, IsDeleted)` | ResolveAsync: filter by Id and IsDeleted for role name. |

---

## 3. **Bases**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_Bases_Cmd_IsDeleted` | `(Cmd, IsDeleted)` | ResolveAsync: command-level user’s allowed bases; NotGroupedProperties and scope filters. |
| `IX_Bases_Id` | *(PK)* | Joins: `c.BaseId = b.Id`, `pg.BaseId = b.Id`, etc. (PK usually sufficient). |

---

## 4. **Commands**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_Commands_Id_IsDeleted` | `(Id, IsDeleted)` | All GETs join on `CmdId = cmd.Id` and filter IsDeleted. Covering Id + IsDeleted speeds lookups. |

*If PK on `Id` exists, consider only if you add a filter index; otherwise PK covers join.*

---

## 5. **Classes**

| Index | Columns | Rationale |
|-------|---------|-----------|
| *(PK on Id)* | - | Joins only on `ClassId = cls.Id`. PK is enough unless Classes is huge. |

---

## 6. **Contracts**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_Contracts_IsDeleted_Id` | `(IsDeleted, Id DESC)` | List GET: filter IsDeleted + order by Id DESC + paging. |
| `IX_Contracts_CmdId_IsDeleted` | `(CmdId, IsDeleted)` | Scope filter (command-level). |
| `IX_Contracts_BaseId_IsDeleted` | `(BaseId, IsDeleted)` | Scope filter (base-level). |
| `IX_Contracts_GrpId_IsDeleted` | `(GrpId, IsDeleted)` | Join with PropertyGroups; SearchByGrpName. |
| `IX_Contracts_Status_ContractStartDate_ContractEndDate` | `(IsDeleted, Status, ContractStartDate, ContractEndDate)` | NotGroupedProperties: active contracts in date range. |

---

## 7. **ContractRiseTerms**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_ContractRiseTerms_ContractId_IsDeleted` | `(ContractId, IsDeleted)` | Subquery per contract in Contracts GET; GetByContractId; scope filter in ContractRiseTermsController. |
| `IX_ContractRiseTerms_ContractId_SequenceNo` | `(ContractId, SequenceNo)` | Order by SequenceNo for a contract. |

---

## 8. **PropertyGroups**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_PropertyGroups_IsDeleted_Status_Id` | `(IsDeleted, Status, Id DESC)` | List GET filter + order; NotGroupedProperties active groups. |
| `IX_PropertyGroups_CmdId_BaseId_IsDeleted` | `(CmdId, BaseId, IsDeleted)` | Scope filter on PropertyGroups. |
| `IX_PropertyGroups_GId_IsDeleted` | `(GId, IsDeleted)` | SearchByGrpName: lookup by GId (group name). |

---

## 9. **PropertyGroupLinkings**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_PropertyGroupLinkings_GrpId_IsDeleted_Status` | `(GrpId, IsDeleted, Status)` | NotGroupedProperties: linked props in active groups; contracted props. |
| `IX_PropertyGroupLinkings_PropId_IsDeleted` | `(PropId, IsDeleted)` | Exclude already-linked property IDs. |

---

## 10. **RentalProperties**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_RentalProperties_IsDeleted_CmdId_BaseId` | `(IsDeleted, CmdId, BaseId)` | Scope filter; NotGroupedProperties filter by CmdId, BaseId. |
| `IX_RentalProperties_Id` | *(PK)* | Joins RevenueRates.PropertyId = p.Id. |

---

## 11. **RevenueRates**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_RevenueRates_PropertyId_IsDeleted_Status` | `(PropertyId, IsDeleted, Status)` | Join from Contracts/PropertyGroup to properties; “latest rate” per property in NotGroupedProperties. |
| `IX_RevenueRates_IsDeleted_Id_DESC` | `(IsDeleted, Id DESC)` | List GET: filter IsDeleted + order by Id DESC + paging. |
| `IX_RevenueRates_PropertyId_ApplicableDate_Id` | `(PropertyId, ApplicableDate DESC, Id DESC)` | NotGroupedProperties: latest rate per property (ApplicableDate desc, then Id desc). |

---

## 12. **SharingFormulas**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_SharingFormulas_IsDeleted_CmdId_BaseId` | `(IsDeleted, CmdId, BaseId)` | Scope filter + list filter. |
| `IX_SharingFormulas_Id_IsDeleted` | `(Id, IsDeleted)` | GetById. |

---

## 13. **RentalValueGovtShareRates**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_RentalValueGovtShareRates_IsDeleted_Status_Type_Id` | `(IsDeleted, Status, Type, Id DESC)` | List GET: filter IsDeleted, Status, optional Type; order Id DESC; scope uses CmdId/BaseId. |
| `IX_RentalValueGovtShareRates_CmdId_BaseId_IsDeleted` | `(CmdId, BaseId, IsDeleted)` | Scope filter (ApplyScope). |

---

## 14. **FileUploads**

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_FileUploads_FormId_IsDeleted` | `(FormId, IsDeleted)` | AttachmentFlagHelper: all GETs that resolve IsAttachment by FormId list. |
| `IX_FileUploads_FormId_FormName_IsDeleted` | `(FormId, FormName, IsDeleted)` | When FormName filter is used (Contracts, RevenueRates, etc.). |

---

## 15. **PropertyTypes**

| Index | Columns | Rationale |
|-------|---------|-----------|
| *(PK on Id)* | - | Join only on PropertyType = pt.Id. |

---

## 16. **AuditLog** (optional, for reporting)

| Index | Columns | Rationale |
|-------|---------|-----------|
| `IX_AuditLog_ActionDateTime_DESC` | `(ActionDateTime DESC)` | If you add GET/reporting by date range. |
| `IX_AuditLog_EntityName_EntityId` | `(EntityName, EntityId)` | Lookup by entity. |

---

# SQL Script (Create Indexes)

Run in the order below. Omit any index that already exists or conflicts with your naming. Table names assume **dbo** schema and EF default/configured names (`RentalProperties`, `PropertyGroups`, `ContractRiseTerms`, `UserNotes`, `AuditLog` as configured; others use DbSet pluralized names).

```sql
-- Users
CREATE NONCLUSTERED INDEX IX_Users_Username ON dbo.Users (Username);
CREATE NONCLUSTERED INDEX IX_Users_LevelId ON dbo.Users (LevelId);

-- Roles
CREATE NONCLUSTERED INDEX IX_Roles_Id_IsDeleted ON dbo.Roles (Id, IsDeleted);

-- Bases
CREATE NONCLUSTERED INDEX IX_Bases_Cmd_IsDeleted ON dbo.Bases (Cmd, IsDeleted);

-- Commands (optional if PK on Id is used for joins only)
-- CREATE NONCLUSTERED INDEX IX_Commands_Id_IsDeleted ON dbo.Commands (Id, IsDeleted);

-- Contracts
CREATE NONCLUSTERED INDEX IX_Contracts_IsDeleted_Id ON dbo.Contracts (IsDeleted, Id DESC);
CREATE NONCLUSTERED INDEX IX_Contracts_CmdId_IsDeleted ON dbo.Contracts (CmdId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_Contracts_BaseId_IsDeleted ON dbo.Contracts (BaseId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_Contracts_GrpId_IsDeleted ON dbo.Contracts (GrpId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_Contracts_Status_DateRange ON dbo.Contracts (IsDeleted, Status, ContractStartDate, ContractEndDate);

-- ContractRiseTerms
CREATE NONCLUSTERED INDEX IX_ContractRiseTerms_ContractId_IsDeleted ON dbo.ContractRiseTerms (ContractId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_ContractRiseTerms_ContractId_SequenceNo ON dbo.ContractRiseTerms (ContractId, SequenceNo);

-- PropertyGroups
CREATE NONCLUSTERED INDEX IX_PropertyGroups_IsDeleted_Status_Id ON dbo.PropertyGroups (IsDeleted, Status, Id DESC);
CREATE NONCLUSTERED INDEX IX_PropertyGroups_CmdId_BaseId_IsDeleted ON dbo.PropertyGroups (CmdId, BaseId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_PropertyGroups_GId_IsDeleted ON dbo.PropertyGroups (GId, IsDeleted);

-- PropertyGroupLinkings
CREATE NONCLUSTERED INDEX IX_PropertyGroupLinkings_GrpId_IsDeleted_Status ON dbo.PropertyGroupLinkings (GrpId, IsDeleted, Status);
CREATE NONCLUSTERED INDEX IX_PropertyGroupLinkings_PropId_IsDeleted ON dbo.PropertyGroupLinkings (PropId, IsDeleted);

-- RentalProperties
CREATE NONCLUSTERED INDEX IX_RentalProperties_IsDeleted_CmdId_BaseId ON dbo.RentalProperties (IsDeleted, CmdId, BaseId);

-- RevenueRates
CREATE NONCLUSTERED INDEX IX_RevenueRates_PropertyId_IsDeleted_Status ON dbo.RevenueRates (PropertyId, IsDeleted, Status);
CREATE NONCLUSTERED INDEX IX_RevenueRates_IsDeleted_Id ON dbo.RevenueRates (IsDeleted, Id DESC);
CREATE NONCLUSTERED INDEX IX_RevenueRates_PropertyId_ApplicableDate_Id ON dbo.RevenueRates (PropertyId, ApplicableDate DESC, Id DESC);

-- SharingFormulas
CREATE NONCLUSTERED INDEX IX_SharingFormulas_IsDeleted_CmdId_BaseId ON dbo.SharingFormulas (IsDeleted, CmdId, BaseId);
CREATE NONCLUSTERED INDEX IX_SharingFormulas_Id_IsDeleted ON dbo.SharingFormulas (Id, IsDeleted);

-- RentalValueGovtShareRates
CREATE NONCLUSTERED INDEX IX_RentalValueGovtShareRates_IsDeleted_Status_Type_Id ON dbo.RentalValueGovtShareRates (IsDeleted, Status, Type, Id DESC);
CREATE NONCLUSTERED INDEX IX_RentalValueGovtShareRates_CmdId_BaseId_IsDeleted ON dbo.RentalValueGovtShareRates (CmdId, BaseId, IsDeleted);

-- FileUploads
CREATE NONCLUSTERED INDEX IX_FileUploads_FormId_IsDeleted ON dbo.FileUploads (FormId, IsDeleted);
CREATE NONCLUSTERED INDEX IX_FileUploads_FormId_FormName_IsDeleted ON dbo.FileUploads (FormId, FormName, IsDeleted);

-- AuditLog (optional)
-- CREATE NONCLUSTERED INDEX IX_AuditLog_ActionDateTime ON dbo.AuditLog (ActionDateTime DESC);
-- CREATE NONCLUSTERED INDEX IX_AuditLog_EntityName_EntityId ON dbo.AuditLog (EntityName, EntityId);
```

---

## Notes

- **Primary keys**: If a table already has a clustered PK on `Id`, the “Id” in composite indexes above can still help when the leading columns are used in WHERE/ORDER.
- **IsDeleted**: Most entities use `IsDeleted = 0 or null` on every GET; including `IsDeleted` in the index (or as first column) helps.
- **Scope (CmdId / BaseId)**: Used in ApplyScope on many entities; indexes on (CmdId, …) and (BaseId, …) or (CmdId, BaseId, …) improve list GETs for command/base users.
- **Joins**: Foreign key columns (ContractId, GrpId, PropertyId, FormId, etc.) benefit from indexes for join and subquery performance.
- Test in a non-production environment first and monitor execution plans and index usage after deployment.
