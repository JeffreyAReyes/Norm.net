﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Norm.Extensions;
using Xunit;

namespace SQLiteUnitTests
{
    public record TestRecord(long Id, string Foo, string Day, long? Bool, string Bar);

    [Collection("SQLiteDatabase")]
    public class QueryRecordsUnitTests
    {
        private readonly SqLiteFixture fixture;

        private const string Query = @"
            with cte(id, foo, day, bool, bar) as (
            select * from (
                values
                  (1, 'foo1', date('1977-05-19'), true, null),
                  (2, 'foo2', date('1978-05-19'), false, 'bar2'),
                  (3, 'foo3', date('1979-05-19'), null, 'bar3')
                )
            )
            select * from cte";

        public QueryRecordsUnitTests(SqLiteFixture fixture)
        {
            this.fixture = fixture;
        }

        private void AssertTestRecord(IList<TestRecord> result)
        {
            Assert.Equal(3, result.Count);

            Assert.Equal(1, result[0].Id);
            Assert.Equal(2, result[1].Id);
            Assert.Equal(3, result[2].Id);

            Assert.Equal("foo1", result[0].Foo);
            Assert.Equal("foo2", result[1].Foo);
            Assert.Equal("foo3", result[2].Foo);

            Assert.Equal("1977-05-19", result[0].Day);
            Assert.Equal("1978-05-19", result[1].Day);
            Assert.Equal("1979-05-19", result[2].Day);

            Assert.Equal(1, result[0].Bool);
            Assert.Equal(0, result[1].Bool);
            Assert.Null(result[2].Bool);

            Assert.Null(result[0].Bar);
            Assert.Equal("bar2", result[1].Bar);
            Assert.Equal("bar3", result[2].Bar);
        }



        [Fact]
        public void SelectMap_Sync()
        {
            using var connection = new SQLiteConnection(fixture.ConnectionString);
            var result = connection.Query<TestRecord>(Query).ToList();
            AssertTestRecord(result);
        }

        [Fact]
        public void SelectEmpty_Sync()
        {
            using var connection = new SQLiteConnection(fixture.ConnectionString);
            var result = connection.Query<TestRecord>($"select * from ({Query}) q where id = 999").ToList();
            Assert.Empty(result);
        }


        [Fact]
        public async Task SelectMap_Async()
        {
            await using var connection = new SQLiteConnection(fixture.ConnectionString);
            var result = await connection.QueryAsync<TestRecord>(Query).ToListAsync();
            AssertTestRecord(result);
        }
    }
}
