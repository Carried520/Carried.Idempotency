local key = KEYS[1]
local ownerToken = ARGV[1]
local leaseDurationMs = tonumber(ARGV[2])

local time = redis.call("TIME")
local nowMs =
    (tonumber(time[1]) * 1000) +
    math.floor(tonumber(time[2]) / 1000)

local entry = redis.call(
    "HMGET",
    key,
    "state",
    "owner_token",
    "expires_at"
)

local state = entry[1]
local storedOwnerToken = entry[2]
local storedExpiresAt = tonumber(entry[3])

if not state then
    return 0
end

if state ~= "in_progress" and state ~= "completed" then
    return redis.error_reply("Invalid idempotency entry state")
end

if not storedExpiresAt then
    return redis.error_reply("Invalid idempotency entry")
end

if state == "completed" then
    return 0
end

if not storedOwnerToken then
    return redis.error_reply("Invalid idempotency entry")
end

if storedOwnerToken ~= ownerToken then
    return 0
end

if storedExpiresAt <= nowMs then
    return 0
end

local renewedExpiresAt = nowMs + leaseDurationMs

redis.call(
    "HSET",
    key,
    "expires_at", renewedExpiresAt
)

redis.call("PEXPIRE", key, leaseDurationMs)

return 1