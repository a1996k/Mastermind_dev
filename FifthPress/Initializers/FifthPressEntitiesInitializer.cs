using System;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace FifthPress.Initializers
{
    public class FifthPressEntitiesInitializer : IDatabaseInitializer<FifthPressEntities>
    {
        public void InitializeDatabase(FifthPressEntities context)
        {
            if (!context.Database.Exists())
            {
                context.Database.Create();
            }
        }

        public FifthPressEntitiesInitializer(FifthPressEntities context)
        {
            InitializeDatabase(context);
        }
    }
}