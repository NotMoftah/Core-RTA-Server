using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization;
using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using System;


namespace CoreRTA.Database
{

    public static class RtsDb
    {
        public static string DatabaseName { get; private set; }
        public static string EndPoint { get; private set; }

        private static MongoClient dbClient;
        private static IMongoDatabase database;



        static RtsDb()
        {
            DatabaseName = "CoreRtsApp";
            EndPoint = "mongodb://localhost:27017/";
        }

        public static void Connect(string dbName)
        {
            DatabaseName = dbName;
            EndPoint = "mongodb://localhost:27017/";

            dbClient = new MongoClient(EndPoint);
            database = dbClient.GetDatabase(DatabaseName);
        }

        public static void Connect(string dbName, string remoteEnd)
        {
            EndPoint = remoteEnd;
            DatabaseName = dbName;

            dbClient = new MongoClient(EndPoint);
            database = dbClient.GetDatabase(DatabaseName);
        }


        #region Collections and Mapping

        public static void AddClassToMap(Type classType)
        {
            if (!BsonClassMap.IsClassMapRegistered(classType))
            {
                BsonClassMap.LookupClassMap(classType);
            }
        }

        public static async void CreateCollection(string name)
        {
            var filter = new BsonDocument("name", name);
            var options = new ListCollectionNamesOptions { Filter = filter };

            if ((await database.ListCollectionNamesAsync(options)).Any() == false)
            {
                await database.CreateCollectionAsync(name);

                var collection = database.GetCollection<Record>(name);

                var indexOptions = new CreateIndexOptions() { Unique = true };
                var collectionBuidler = Builders<Record>.IndexKeys;
                var indexModel = new CreateIndexModel<Record>(collectionBuidler.Ascending(x => x.key), indexOptions);

                await collection.Indexes.CreateOneAsync(indexModel);
            }
        }

        #endregion


        #region Unique Documents

        public static async void UpdateUniqueDocument(string collection, string key, object obj)
        {
            var coll = database.GetCollection<Record>(collection);
            var filter = Builders<Record>.Filter.Eq("key", key);
            var options = new UpdateOptions { IsUpsert = true };

            var json = JsonSerializer.Serialize(obj);
            var payload = BsonDocument.Parse(json);

            var update = Builders<Record>.Update.Set("payload", payload)
                                                      .Set("lastModificationDate", DateTime.Now);

            await coll.UpdateOneAsync(filter, update, options);
        }

        public static async void DeleteUniqueDocument(string collection, string key)
        {
            var coll = database.GetCollection<Record>(collection);

            var filter = Builders<Record>.Filter.Eq("key", key);

            await coll.DeleteManyAsync(filter);
        }

        public static T ReadUniqueDocument<T>(string collectionName, string key)
        {
            AddClassToMap(typeof(T));

            var collection = database.GetCollection<Record>(collectionName);

            var result = collection.Find(x => x.key == key).FirstOrDefault();

            if (result != null)
                return BsonSerializer.Deserialize<T>(result.payload);

            return default(T);
        }

        #endregion

    }

}
