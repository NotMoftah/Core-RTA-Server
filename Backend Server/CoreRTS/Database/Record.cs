using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Bson;
using System;


namespace CoreRTA.Database
{

    [BsonIgnoreExtraElements]
    class Record
    {
        public string key = string.Empty;
        public BsonDocument payload = null;
        public ObjectId _id = ObjectId.Empty;
        public DateTime lastModificationDate = DateTime.Now;

    }

}