﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Util.Datas.Queries;
using Util.Datas.Sql.Queries.Builders.Conditions;
using Util.Helpers;
using Util.Properties;

namespace Util.Datas.Sql.Queries.Builders {
    /// <summary>
    /// Sql生成器
    /// </summary>
    public abstract class SqlBuilderBase : ISqlBuilder {

        #region 字段

        /// <summary>
        /// 参数集合
        /// </summary>
        private readonly IDictionary<string, object> _params;
        /// <summary>
        /// 参数索引
        /// </summary>
        private int _paramIndex = 0;

        #endregion

        #region 构造方法

        /// <summary>
        /// 初始化Sql生成器
        /// </summary>
        protected SqlBuilderBase() {
            _params = new Dictionary<string, object>();
            Columns = new List<SqlItem>();
        }

        #endregion

        #region 属性

        /// <summary>
        /// 参数标识，用于防止多个Sql生成器生成的参数重复
        /// </summary>
        public string Tag { get; set; }
        /// <summary>
        /// 子生成器数量,用于生成子生成器的Tag
        /// </summary>
        protected int ChildBuilderCount { get; set; } = 0;
        /// <summary>
        /// 架构
        /// </summary>
        protected string Schema { get; private set; }
        /// <summary>
        /// 表名
        /// </summary>
        protected string Table { get; private set; }
        /// <summary>
        /// 别名
        /// </summary>
        protected string Alias { get; private set; }
        /// <summary>
        /// 列名集合
        /// </summary>
        protected List<SqlItem> Columns { get; private set; }
        /// <summary>
        /// 查询条件
        /// </summary>
        protected ICondition Condition { get; private set; }

        #endregion

        #region New(创建Sql生成器)

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        public abstract ISqlBuilder New();

        #endregion

        #region ToSql(生成Sql语句)

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        public string ToSql() {
            Validate();
            var result = new StringBuilder();
            CreateSql( result );
            var sql = result.ToString().Trim();
            WriteTraceLog( sql, GetParams() );
            return sql;
        }

        /// <summary>
        /// 验证
        /// </summary>
        public void Validate() {
            if( string.IsNullOrWhiteSpace( Table ) )
                throw new InvalidOperationException( LibraryResource.TableIsEmpty );
        }

        /// <summary>
        /// 创建Sql语句
        /// </summary>
        protected abstract void CreateSql( StringBuilder result );

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="parameters">参数</param>
        protected abstract void WriteTraceLog( string sql, IDictionary<string, object> parameters );

        #endregion

        #region GetParams(获取参数)

        /// <summary>
        /// 获取参数
        /// </summary>
        public IDictionary<string, object> GetParams() {
            return _params;
        }

        #endregion

        #region Select(设置列名)

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名</param>
        /// <param name="tableAlias">表别名</param>
        public virtual ISqlBuilder Select( string columns, string tableAlias = null ) {
            if( string.IsNullOrWhiteSpace( columns ) )
                return this;
            Columns.AddRange( columns.Split( ',' ).Select( column => new SqlItem( column, tableAlias ) ) );
            return this;
        }

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名</param>
        /// <param name="tableAlias">表别名</param>
        public virtual ISqlBuilder Select<TEntity>( Expression<Func<TEntity, object[]>> columns, string tableAlias = null ) {
            if( columns == null )
                return this;
            return Select( Lambda.GetNames( columns ).Join(), tableAlias );
        }

        #endregion

        #region From(设置表名)

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        public ISqlBuilder From( string table, string alias = null, string schema = null ) {
            Table = table;
            Alias = alias;
            Schema = schema;
            return this;
        }

        #endregion

        #region And(And连接条件)

        /// <summary>
        /// And连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        public ISqlBuilder And( ICondition condition ) {
            Condition = new AndCondition( Condition, condition );
            return this;
        }

        #endregion

        #region Where(设置条件)

        /// <summary>
        /// 设置条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <param name="tableAlias">表别名</param>
        public ISqlBuilder Where( string column, object value, Operator @operator = Operator.Equal, string tableAlias = null ) {
            if( string.IsNullOrWhiteSpace( column ) )
                throw new ArgumentNullException( nameof( column ) );
            column = GetColumn( new SqlItem( column, tableAlias ) );
            var paramName = GetParamName();
            And( SqlConditionFactory.Create( column, paramName, @operator ) );
            AddParam( paramName, value, @operator );
            return this;
        }

        /// <summary>
        /// 获取参数名
        /// </summary>
        protected virtual string GetParamName() {
            return $"{GetPrefix()}_p_{Tag}_{_paramIndex++}";
        }

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        protected abstract string GetPrefix();

        /// <summary>
        /// 添加参数
        /// </summary>
        protected virtual void AddParam( string parameterName, object value, Operator @operator ) {
            _params.Add( parameterName, GetValue( value, @operator ) );
        }

        /// <summary>
        /// 获取值
        /// </summary>
        protected virtual object GetValue( object value, Operator @operator ) {
            switch( @operator ) {
                case Operator.Contains:
                    return $"%{value}%";
                case Operator.Starts:
                    return $"{value}%";
                case Operator.Ends:
                    return $"%{value}";
                default:
                    return value;
            }
        }

        #endregion

        #region 获取子句

        /// <summary>
        /// 获取Select子句
        /// </summary>
        protected virtual string GetSelect() {
            return $"Select {GetColumns()} ";
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        protected virtual string GetColumns() {
            if( Columns.Count == 0 )
                return "*";
            return Columns.Select( GetColumn ).Join();
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        protected virtual string GetColumn( SqlItem item ) {
            if( item == null )
                return string.Empty;
            var column = $"{GetAlias( item.Prefix )}.{SafeName( item.Name )}";
            if( string.IsNullOrWhiteSpace( item.Alias ) )
                return column;
            return $"{column} As {SafeName( item.Alias )}";
        }

        /// <summary>
        /// 获取安全名称
        /// </summary>
        protected abstract string SafeName( string name );

        /// <summary>
        /// 获取表别名
        /// </summary>
        protected string GetAlias( string alias = null ) {
            if( string.IsNullOrWhiteSpace( alias ) == false )
                return SafeName( alias );
            if( string.IsNullOrWhiteSpace( Alias ) )
                return SafeName( "t" );
            return SafeName( Alias );
        }

        /// <summary>
        /// 获取From子句
        /// </summary>
        protected virtual string GetFrom() {
            var item = new SqlItem( Table, Schema, Alias );
            return $"From {GetTable( item )} As {GetAlias( item.Alias )} ";
        }

        /// <summary>
        /// 获取表名
        /// </summary>
        private string GetTable( SqlItem item ) {
            if( string.IsNullOrWhiteSpace( item.Prefix ) )
                return SafeName( item.Name );
            return $"{SafeName( item.Prefix )}.{SafeName( item.Name )}";
        }

        /// <summary>
        /// 获取Where子句
        /// </summary>
        protected virtual string GetWhere() {
            var condition = GetCondition();
            if( string.IsNullOrWhiteSpace( condition ) )
                return string.Empty;
            return $"Where {condition} ";
        }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public string GetCondition() {
            return Condition?.GetCondition();
        }

        #endregion
    }
}