﻿using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Norm
{
    public partial class Norm
    {
        protected virtual void ApplyOptions(DbCommand cmd)
        {
            if (NormOptions.Value.CommandTimeout.HasValue)
            {
                cmd.CommandTimeout = NormOptions.Value.CommandTimeout.Value;
            }
            if (commandTimeout.HasValue)
            {
                cmd.CommandTimeout = commandTimeout.Value;
            }

            ApllyCommentHeader(cmd);

            NormOptions.Value.DbCommandCallback?.Invoke(cmd);
            dbCommandCallback?.Invoke(cmd);

            if (cmd.CommandType == CommandType.StoredProcedure 
                && ((this.dbType | NormOptions.Value.OmmitStoredProcCommandCommentHeaderForDbTypes) == NormOptions.Value.OmmitStoredProcCommandCommentHeaderForDbTypes))
            {
                if (this.commentHeader != null)
                {
                    cmd.CommandText = cmd.CommandText.Replace(this.commentHeader, "");
                }
            }
        }

        protected virtual void ApllyCommentHeader(DbCommand cmd)
        {
            commandText = cmd.CommandText;
            commentHeader = null;

            if (!NormOptions.Value.CommandCommentHeader.Enabled && !this.commandCommentHeaderEnabled)
            {
                return;
            }
            
            var sb = new StringBuilder();

            if (this.commandCommentHeaderEnabled && this.comment != null)
            {
                sb.AppendLine($"-- {this.comment}");
            }

            if ((NormOptions.Value.CommandCommentHeader.Enabled && NormOptions.Value.CommandCommentHeader.IncludeCommandAttributes) ||
                (this.commandCommentHeaderEnabled && this.includeCommandAttributes))
            {
                sb.AppendLine($"-- {(this.dbType == DatabaseType.Other ? "" : $"{this.dbType} ")}{cmd.CommandType.ToString()} Command. Timeout: {cmd.CommandTimeout} seconds.");
            }

            if ((NormOptions.Value.CommandCommentHeader.Enabled && NormOptions.Value.CommandCommentHeader.IncludeCallerInfo) ||
                (this.commandCommentHeaderEnabled && this.includeCallerInfo))
            {
                sb.AppendLine($"-- at {memberName} in {sourceFilePath} {sourceLineNumber}");
            }

            if ((NormOptions.Value.CommandCommentHeader.Enabled && NormOptions.Value.CommandCommentHeader.IncludeTimestamp) ||
                (this.commandCommentHeaderEnabled && this.includeTimestamp))
            {
                sb.AppendLine($"-- Timestamp: {DateTime.Now:o}");
            }

            if ((NormOptions.Value.CommandCommentHeader.Enabled && NormOptions.Value.CommandCommentHeader.IncludeParameters) ||
                (this.commandCommentHeaderEnabled && this.includeParameters))
            {
                foreach (DbParameter p in cmd.Parameters)
                {
                    string paramType;
                    if (this.dbType == DatabaseType.Other)
                    {
                        paramType = p.DbType.ToString().ToLowerInvariant();
                    }
                    else
                    {
                        var prop = p.GetType().GetProperty($"{this.dbType}DbType");
                        if (prop != null)
                        {
                            paramType = prop.GetValue(p).ToString().ToLowerInvariant();
                        }
                        else
                        {
                            paramType = this.dbType.ToString().ToLowerInvariant();
                        }
                    }
                    object value = p.Value is DateTime time ? time.ToString("o") : p.Value;
                    if (value is string)
                    {
                        value = $"\"{value}\"";
                    }
                    else if (value is bool)
                    {
                        value = value.ToString().ToLowerInvariant();
                    }
                    sb.Append(string.Format(NormOptions.Value.CommandCommentHeader.ParametersFormat, p.ParameterName, paramType, value));
                }
            }

            if (sb.Length > 0)
            {
                commandText = cmd.CommandText;
                commentHeader = sb.ToString();
                cmd.CommandText = string.Concat(commentHeader, commandText);
            }
        }

        protected DbCommand CreateCommand(string command)
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = commandType;
            Connection.EnsureIsOpen();
            if (this.parameters != null)
            {
                AddParametersInternal(cmd, this.parameters);
            }
            if (NormOptions.Value.Prepared || prepared)
            {
                cmd.Prepare();
                prepared = false;
            }
            ApplyOptions(cmd);
            return cmd;
        }

        protected async ValueTask<DbCommand> CreateCommandAsync(string command)
        {
            cancellationToken?.ThrowIfCancellationRequested();
            var cmd = Connection.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = commandType;
            await Connection.EnsureIsOpenAsync(cancellationToken);
            if (this.parameters != null)
            {
                AddParametersInternal(cmd, this.parameters);
            }
            if (NormOptions.Value.Prepared || prepared)
            {
                if (cancellationToken.HasValue)
                {
                    await cmd.PrepareAsync(cancellationToken.Value);
                }
                else
                {
                    await cmd.PrepareAsync();
                }
                prepared = false;
            }
            ApplyOptions(cmd);
            return cmd;
        }

        protected DbCommand CreateCommand(FormattableString command)
        {
            var (commandString, parameters) = ParseFormattableCommand(command);
            if (parameters.Length > 0)
            {
                this.parameters = parameters;
            }
            return CreateCommand(commandString);
        }
        
        protected async ValueTask<DbCommand> CreateCommandAsync(FormattableString command)
        {
            var (commandString, parameters) = ParseFormattableCommand(command);
            if (parameters.Length > 0)
            {
                this.parameters = parameters;
            }
            return await CreateCommandAsync(commandString);
        }
    }
}