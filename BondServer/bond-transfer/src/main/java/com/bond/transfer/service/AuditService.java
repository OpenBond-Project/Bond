package com.bond.transfer.service;

import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import com.bond.common.dto.AuditLogDTO;
import com.bond.common.dto.PageResult;
import com.bond.common.entity.AuditLog;
import com.bond.common.utils.SnowflakeId;
import com.bond.transfer.mapper.AuditLogMapper;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * 管理操作审计日志服务（任务单 #19，V15）。
 *
 * <p>只写与查询，不提供删除；写失败只 warn，绝不阻断被审计的管理操作。</p>
 */
@Service
@Slf4j
@RequiredArgsConstructor
public class AuditService {

    private static final int MAX_PAGE_SIZE = 100;
    private static final int MAX_ACTION_LENGTH = 64;
    private static final int MAX_TARGET_TYPE_LENGTH = 32;
    private static final int MAX_DETAIL_LENGTH = 4000;
    private static final int MAX_IP_LENGTH = 64;
    private static final int MAX_ACTOR_NAME_LENGTH = 64;

    private final AuditLogMapper auditLogMapper;

    public void record(Long actorId, String actorName, String action, String targetType,
                       Long targetId, String detailJson, String ip) {
        if (actorId == null || action == null || action.isBlank()) {
            return;
        }
        try {
            AuditLog row = new AuditLog();
            row.setId(SnowflakeId.nextId());
            row.setActorId(actorId);
            row.setActorName(truncate(actorName, MAX_ACTOR_NAME_LENGTH));
            row.setAction(truncate(action, MAX_ACTION_LENGTH));
            row.setTargetType(truncate(targetType, MAX_TARGET_TYPE_LENGTH));
            row.setTargetId(targetId);
            row.setDetail(truncate(detailJson, MAX_DETAIL_LENGTH));
            row.setIp(truncate(ip, MAX_IP_LENGTH));
            auditLogMapper.insertLog(row);
        } catch (RuntimeException e) {
            log.warn("audit log write failed: action={}, actorId={}, error={}",
                    action, actorId, e.toString());
        }
    }

    /**
     * 分页查询审计日志。
     *
     * @param actorId      按操作人 ID 精确过滤
     * @param actorKeyword 按操作人模糊过滤：匹配 {@code actor_name}（写入时的昵称/用户名快照）
     *                     或 {@code actor_id} 的文本片段，方便直接拿用户名查
     */
    public PageResult<AuditLogDTO> list(int page, int size, String action, Long actorId,
                                        String actorKeyword, OffsetDateTime from, OffsetDateTime to) {
        int safePage = Math.max(page, 1);
        int safeSize = Math.min(Math.max(size, 1), MAX_PAGE_SIZE);
        LambdaQueryWrapper<AuditLog> wrapper = new LambdaQueryWrapper<>();
        if (action != null && !action.isBlank()) {
            wrapper.eq(AuditLog::getAction, action.trim());
        }
        if (actorId != null) {
            wrapper.eq(AuditLog::getActorId, actorId);
        }
        applyActorKeyword(wrapper, actorKeyword);
        if (from != null) {
            wrapper.ge(AuditLog::getCreatedAt, from);
        }
        if (to != null) {
            wrapper.le(AuditLog::getCreatedAt, to);
        }
        wrapper.orderByDesc(AuditLog::getCreatedAt)
                .last("LIMIT " + safeSize + " OFFSET " + (long) (safePage - 1) * safeSize);
        List<AuditLog> rows = auditLogMapper.selectList(wrapper);
        long total = count(action, actorId, actorKeyword, from, to);
        List<AuditLogDTO> items = rows.stream().map(AuditService::toDto).toList();
        return PageResult.of(items, total, safePage, safeSize);
    }

    /** 操作人模糊匹配：actor_name ILIKE %kw% 或 actor_id 文本包含 kw（用户名/昵称/ID 片段都能查）。 */
    private static void applyActorKeyword(LambdaQueryWrapper<AuditLog> wrapper, String actorKeyword) {
        if (actorKeyword == null || actorKeyword.isBlank()) {
            return;
        }
        String kw = actorKeyword.trim();
        wrapper.and(w -> w.like(AuditLog::getActorName, kw)
                .or().apply("CAST(actor_id AS TEXT) LIKE {0}", "%" + kw + "%"));
    }

    private long count(String action, Long actorId, String actorKeyword,
                       OffsetDateTime from, OffsetDateTime to) {
        LambdaQueryWrapper<AuditLog> wrapper = new LambdaQueryWrapper<>();
        if (action != null && !action.isBlank()) {
            wrapper.eq(AuditLog::getAction, action.trim());
        }
        if (actorId != null) {
            wrapper.eq(AuditLog::getActorId, actorId);
        }
        applyActorKeyword(wrapper, actorKeyword);
        if (from != null) {
            wrapper.ge(AuditLog::getCreatedAt, from);
        }
        if (to != null) {
            wrapper.le(AuditLog::getCreatedAt, to);
        }
        return auditLogMapper.selectCount(wrapper);
    }

    public List<String> actions() {
        return auditLogMapper.selectActions();
    }

    private static AuditLogDTO toDto(AuditLog row) {
        AuditLogDTO dto = new AuditLogDTO();
        dto.setId(row.getId());
        dto.setActorId(row.getActorId());
        dto.setActorName(row.getActorName());
        dto.setAction(row.getAction());
        dto.setTargetType(row.getTargetType());
        dto.setTargetId(row.getTargetId());
        dto.setDetail(row.getDetail());
        dto.setIp(row.getIp());
        dto.setCreatedAtMillis(row.getCreatedAt() == null ? null : row.getCreatedAt().toInstant().toEpochMilli());
        return dto;
    }

    private static String truncate(String value, int max) {
        if (value == null || value.length() <= max) {
            return value;
        }
        return value.substring(0, max);
    }
}
